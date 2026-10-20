using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço responsável exclusivamente pelas formas de pagamento / parcelas do pedido (TabEst3C).
/// Alimenta a tabela de quitação e contas a receber exatamente como o ERP da CompuServe.
/// </summary>
public class PedidoPagService
{
    public async Task<IReadOnlyList<FormaPagamento>> ObterPagamentosPorPedidoAsync(int pedidoNum)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sqlPagamentos = @"
            SELECT
                tc.Controle,
                tc.PEDIDO as Pedido,
                tc.RECEBIMENTO as Recebimento,
                tc.LkTipo,
                tc.DATA as Data,
                tc.TOTAL as Total,
                tc.VALOR as Valor,
                tc.PARCELA as Parcela,
                tc.CAIXA as Caixa,
                tc.LKCAIXA as LkCaixa,
                tc.OPERADOR as Operador,
                tc.LKOPERADOR as LkOperador,
                tc.HORA as Hora,
                tc.GERACONTA as GeraConta,
                tc.VENCIMENTO as Vencimento,
                tc.ATIVATROCO as AtivaTroco,
                tc.LKTROCO as LkTroco,
                tc.TIPOTROCO as TipoTroco,
                tc.VLTROCO as VlTroco,
                tc.idEmpresa as IdEmpresa,
                tc.LkConta,
                tc.Conta,
                tc.NumDoc,
                tc.divida as Divida,
                tc.VlFatura,
                tc.cAut as CAut
            FROM TabEst3C tc
            WHERE tc.PEDIDO = @pedidoNum
            ORDER BY tc.PARCELA, tc.Controle";

        var list = await conn.QueryAsync<FormaPagamento>(sqlPagamentos, new { pedidoNum });
        return list.ToList();
    }

    public Task SalvarPagamentosAsync(
        int pedidoNum,
        decimal totalPedido,
        IEnumerable<FormaPagamento> pagamentos,
        IDbConnection conn,
        IDbTransaction trans) => SalvarPagamentosAsync(pedidoNum, totalPedido, pagamentos, 2, conn, trans);

    public async Task SalvarPagamentosAsync(
        int pedidoNum,
        decimal totalPedido,
        IEnumerable<FormaPagamento> pagamentos,
        int idEmpresa,
        IDbConnection conn,
        IDbTransaction trans)
    {
        // 1. Remove pagamentos anteriores para atualização consistente
        await ExcluirPagamentosPorPedidoAsync(pedidoNum, conn, trans);

        const string sqlNewCodC = @"
            DECLARE @NewCod INT;
            BEGIN TRY
                EXEC NewCod_TabEst3c @NewCod OUTPUT;
            END TRY
            BEGIN CATCH
                SELECT @NewCod = ISNULL(MAX(Controle), 0) + 1 FROM TabEst3C;
            END CATCH
            SELECT @NewCod;";

        const string sqlInsertC = @"
            INSERT INTO TabEst3C (
                Controle, PEDIDO, RECEBIMENTO, LkTipo, DATA,
                TOTAL, VALOR, PARCELA, CAIXA, LKCAIXA,
                OPERADOR, LKOPERADOR, HORA, GERACONTA, VENCIMENTO,
                ATIVATROCO, LKTROCO, TIPOTROCO, VLTROCO, idEmpresa,
                LkConta, Conta, NumDoc, divida, VlFatura, cAut
            ) VALUES (
                @Controle, @Pedido, @Recebimento, @LkTipo, @Data,
                @Total, @Valor, @Parcela, @Caixa, @LkCaixa,
                @Operador, @LkOperador, @Hora, @GeraConta, @Vencimento,
                @AtivaTroco, @LkTroco, @TipoTroco, @VlTroco, @IdEmpresa,
                @LkConta, @Conta, @NumDoc, @Divida, @VlFatura, @CAut
            );";

        foreach (var pag in pagamentos.Where(p => p.Valor > 0))
        {
            pag.Controle = await conn.ExecuteScalarAsync<int>(sqlNewCodC, transaction: trans);
            pag.Pedido = pedidoNum;
            pag.Valor = Math.Round(pag.Valor, 2);
            pag.Total = Math.Round(totalPedido, 2);
            pag.NumDoc = pedidoNum.ToString();
            pag.VlFatura = Math.Round(totalPedido, 2);
            pag.Divida = pag.Valor;

            if (pag.Data == default) pag.Data = DateTime.Today;
            if (pag.Vencimento == default) pag.Vencimento = DateTime.Today;
            if (string.IsNullOrEmpty(pag.Hora)) pag.Hora = DateTime.Now.ToString("HH:mm");
            pag.IdEmpresa = idEmpresa > 0 ? idEmpresa : (pag.IdEmpresa > 0 ? pag.IdEmpresa : 2);

            // Regra do ERP Legado: Quando pagamento for Dinheiro, grava TIPOTROCO = 'DINHEIRO'
            if (string.Equals(pag.Recebimento, "DINHEIRO", StringComparison.OrdinalIgnoreCase) || pag.LkTipo == 1)
            {
                if (string.IsNullOrWhiteSpace(pag.TipoTroco))
                {
                    pag.TipoTroco = "DINHEIRO";
                }
            }

            if (pag.Recebimento?.Length > 40) pag.Recebimento = pag.Recebimento.Substring(0, 40);
            if (pag.Caixa?.Length > 15) pag.Caixa = pag.Caixa.Substring(0, 15);
            if (pag.Operador?.Length > 15) pag.Operador = pag.Operador.Substring(0, 15);
            if (pag.Hora?.Length > 5) pag.Hora = pag.Hora.Substring(0, 5);
            if (pag.TipoTroco?.Length > 15) pag.TipoTroco = pag.TipoTroco.Substring(0, 15);
            if (pag.Conta?.Length > 10) pag.Conta = pag.Conta.Substring(0, 10);
            if (pag.NumDoc?.Length > 20) pag.NumDoc = pag.NumDoc.Substring(0, 20);
            if (pag.CAut?.Length > 20) pag.CAut = pag.CAut.Substring(0, 20);

            await conn.ExecuteAsync(sqlInsertC, pag, transaction: trans);
        }
    }

    public async Task ExcluirPagamentosPorPedidoAsync(int pedidoNum, IDbConnection conn, IDbTransaction trans)
    {
        const string sqlDelete = "DELETE FROM TabEst3C WHERE PEDIDO = @pedidoNum";
        await conn.ExecuteAsync(sqlDelete, new { pedidoNum }, transaction: trans);
    }

    /// <summary>
    /// Remove registros órfãos de TabEst3C cujo pedido já não existe mais em TabEst3A.
    /// </summary>
    public async Task<int> LimparPagamentosOrfaosAsync(IDbConnection conn, IDbTransaction trans)
    {
        const string sqlOrfaos = @"
            DELETE FROM TabEst3C 
            WHERE NOT EXISTS (
                SELECT 1 FROM TabEst3A ta WHERE ta.Pedido = TabEst3C.PEDIDO
            )";
        return await conn.ExecuteAsync(sqlOrfaos, transaction: trans);
    }
}
