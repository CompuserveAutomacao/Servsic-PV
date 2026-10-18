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
                tc.VALOR as Valor,
                tc.PARCELA as Parcela,
                tc.GERACONTA as GeraConta,
                tc.VENCIMENTO as Vencimento,
                tc.CAIXA as Caixa,
                tc.OPERADOR as Operador
            FROM TabEst3C tc
            WHERE tc.PEDIDO = @pedidoNum
            ORDER BY tc.Controle";

        var list = await conn.QueryAsync<FormaPagamento>(sqlPagamentos, new { pedidoNum });
        return list.ToList();
    }

    public async Task SalvarPagamentosAsync(
        int pedidoNum,
        IEnumerable<FormaPagamento> pagamentos,
        IDbConnection conn,
        IDbTransaction trans)
    {
        // 1. Remove pagamentos anteriores para atualização
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
                Controle, PEDIDO, RECEBIMENTO, LkTipo, VALOR,
                PARCELA, GERACONTA, VENCIMENTO, CAIXA, OPERADOR
            ) VALUES (
                @Controle, @Pedido, @Recebimento, @LkTipo, @Valor,
                @Parcela, @GeraConta, @Vencimento, @Caixa, @Operador
            );";

        foreach (var pag in pagamentos)
        {
            pag.Controle = await conn.ExecuteScalarAsync<int>(sqlNewCodC, transaction: trans);
            pag.Pedido = pedidoNum;

            await conn.ExecuteAsync(sqlInsertC, pag, transaction: trans);
        }
    }

    public async Task ExcluirPagamentosPorPedidoAsync(int pedidoNum, IDbConnection conn, IDbTransaction trans)
    {
        const string sqlDelete = "DELETE FROM TabEst3C WHERE PEDIDO = @pedidoNum";
        await conn.ExecuteAsync(sqlDelete, new { pedidoNum }, transaction: trans);
    }
}
