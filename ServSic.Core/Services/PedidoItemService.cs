using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço responsável exclusivamente pelas operações nos itens do pedido/orçamento (TabEst3B).
/// Garante a gravação do vendedor associado ao usuário logado em cada item lançado.
/// </summary>
public class PedidoItemService
{
    public async Task<IReadOnlyList<PedidoItem>> ObterItensPorPedidoAsync(int lkEst3A)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sqlItens = @"
            SELECT
                tb.Controle,
                tb.LkEst3A,
                tb.PEDIDO as Pedido,
                tb.LkProduto,
                tb.CodInterno,
                tb.Produto,
                tb.Quantidade,
                tb.VlUnitario,
                tb.Desconto,
                tb.Acrescimo,
                tb.Total,
                tb.PrecoVenda,
                tb.PrCusto,
                tb.Unidade,
                tb.MARCA as Marca,
                tb.LkVendedor,
                tb.Vendedor,
                ISNULL(tb.Entrega, 0) as Entrega
            FROM TabEst3B tb
            WHERE tb.LkEst3A = @lkEst3A
            ORDER BY tb.Controle";

        var list = await conn.QueryAsync<PedidoItem>(sqlItens, new { lkEst3A });
        return list.ToList();
    }

    public async Task SalvarItensAsync(
        int lkEst3A,
        int pedidoNum,
        IEnumerable<PedidoItem> itens,
        int? lkVendedor,
        string? vendedor,
        int idEmpresa = 2,
        IDbConnection conn = null!,
        IDbTransaction trans = null!)
    {
        // 1. Remove itens anteriores para atualização atômica
        await ExcluirItensPorPedidoAsync(lkEst3A, pedidoNum, conn, trans);

        const string sqlNewCodB = @"
            DECLARE @NewCod INT;
            BEGIN TRY
                EXEC NewCod_TabEst3b @NewCod OUTPUT;
            END TRY
            BEGIN CATCH
                SELECT @NewCod = ISNULL(MAX(Controle), 0) + 1 FROM TabEst3B;
            END CATCH
            SELECT @NewCod;";

        const string sqlInsertB = @"
            INSERT INTO TabEst3B (
                Controle, LkEst3A, PEDIDO, LkProduto, CodInterno, CodOrigem, Produto,
                Quantidade, VlUnitario, Desconto, Acrescimo, Total, PrecoVenda,
                PrCusto, Lucro, Unidade, MARCA, LkVendedor, Vendedor,
                DataInc, HoraIni, Venda_Servico, idEmpresa, CFOP, CST, CstECF,
                QtdSaldo, QtdReal, FracUN, FracQtd, FracQtdTotal, FracPreco,
                QtdAtacado, peso, servico, Cor, Localizacao,
                DescRateio, AcrescRateio, TotalLiquido, QtdDevolvida, lkPreco, LkGuiche, LkBaixa, lkVarejo_Atacado, Entrega
            ) VALUES (
                @Controle, @LkEst3A, @Pedido, @LkProduto, @CodInterno, @CodOrigem, @Produto,
                @Quantidade, @VlUnitario, @Desconto, @Acrescimo, @Total, @PrecoVenda,
                @PrCusto, @Lucro, @Unidade, @Marca, @LkVendedor, @Vendedor,
                @DataInc, @HoraIni, @Venda_Servico, @IdEmpresa, @Cfop, @Cst, @CstEcf,
                @QtdSaldo, @QtdReal, @FracUN, @FracQtd, @FracQtdTotal, @FracPreco,
                @QtdAtacado, @Peso, @Servico, @Cor, @Localizacao,
                @DescRateio, @AcrescRateio, @TotalLiquido, @QtdDevolvida, @LkPreco, @LkGuiche, @LkBaixa, @LkVarejo_Atacado, @Entrega
            );";

        foreach (var item in itens)
        {
            item.Controle = await conn.ExecuteScalarAsync<int>(sqlNewCodB, transaction: trans);
            item.LkEst3A = lkEst3A;
            item.Pedido = pedidoNum;
            // Garante impreterivelmente o vendedor do usuário logado / do cabeçalho
            item.LkVendedor = lkVendedor ?? item.LkVendedor;
            item.Vendedor = !string.IsNullOrWhiteSpace(vendedor) ? vendedor : item.Vendedor;

            // Busca dados fiscais e de custo atualizados caso estejam zerados
            if (item.PrCusto <= 0 || item.Cfop == null || string.IsNullOrWhiteSpace(item.Cst) || string.IsNullOrWhiteSpace(item.CodOrigem))
            {
                var prodInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT PrecoCusto, CodORIGEM, CFOP, CST, Quantidade, Peso FROM TabEst1 WHERE Controle = @LkProduto",
                    new { item.LkProduto }, transaction: trans);
                if (prodInfo != null)
                {
                    if (item.PrCusto <= 0 && prodInfo.PrecoCusto != null) item.PrCusto = Convert.ToDecimal(prodInfo.PrecoCusto);
                    if (string.IsNullOrWhiteSpace(item.CodOrigem) && prodInfo.CodORIGEM != null) item.CodOrigem = Convert.ToString(prodInfo.CodORIGEM);
                    if ((item.Cfop == null || item.Cfop <= 0) && prodInfo.CFOP != null) item.Cfop = Convert.ToInt32(prodInfo.CFOP);
                    if (string.IsNullOrWhiteSpace(item.Cst) && prodInfo.CST != null) item.Cst = Convert.ToString(prodInfo.CST);
                    if (item.QtdSaldo <= 0 && prodInfo.Quantidade != null) item.QtdSaldo = Convert.ToDecimal(prodInfo.Quantidade);
                    if (item.Peso <= 0 && prodInfo.Peso != null) item.Peso = Convert.ToDouble(prodInfo.Peso);
                }
            }

            if (item.Cfop == null || item.Cfop <= 0) item.Cfop = 5405;

            // Tratamento do CST e CstECF:
            // TabEst1.CST armazena valores varchar do legado ('FF', '01', 'NN', etc).
            // Já TabEst3B.CST é obrigatoriamente do tipo INT no banco, enquanto TabEst3B.CstECF é varchar(5).
            string cstRaw = (item.Cst ?? string.Empty).Trim().ToUpperInvariant();
            string cstEcf = !string.IsNullOrWhiteSpace(item.CstEcf) ? item.CstEcf.Trim().ToUpperInvariant() : string.Empty;

            if (string.IsNullOrWhiteSpace(cstEcf))
            {
                cstEcf = (cstRaw == "60" || cstRaw == "FF" || item.Cfop == 5405) ? "FF" : "01";
            }
            item.CstEcf = cstEcf;

            int cstInt;
            if (int.TryParse(cstRaw, out int parsedCst))
            {
                cstInt = parsedCst;
            }
            else
            {
                cstInt = (cstEcf == "FF" || item.Cfop == 5405) ? 60 : 102;
            }
            item.Cst = cstInt.ToString();

            // Sanitização de colunas da TabEst3B
            if (item.Produto?.Length > 80) item.Produto = item.Produto.Substring(0, 80);
            if (item.CodInterno?.Length > 20) item.CodInterno = item.CodInterno.Substring(0, 20);
            if (item.CodOrigem?.Length > 20) item.CodOrigem = item.CodOrigem.Substring(0, 20);
            if (item.Unidade?.Length > 2) item.Unidade = item.Unidade.Substring(0, 2);
            if (item.Marca?.Length > 20) item.Marca = item.Marca.Substring(0, 20);
            if (item.Vendedor?.Length > 30) item.Vendedor = item.Vendedor.Substring(0, 30);

            decimal lucro = Math.Round(item.Total - (item.PrCusto * item.Quantidade), 2);

            var parameters = new DynamicParameters(item);
            parameters.Add("PrecoVenda", item.VlUnitario);
            parameters.Add("Lucro", lucro);
            parameters.Add("IdEmpresa", idEmpresa > 0 ? idEmpresa : 2);
            parameters.Add("CodOrigem", item.CodOrigem ?? string.Empty);
            parameters.Add("Cfop", item.Cfop);
            parameters.Add("Cst", cstInt);
            parameters.Add("CstEcf", item.CstEcf);
            parameters.Add("QtdSaldo", item.QtdSaldo);
            parameters.Add("DataInc", DateTime.Today);
            parameters.Add("HoraIni", DateTime.Now.ToString("HH:mm"));
            parameters.Add("Venda_Servico", "I");
            parameters.Add("QtdReal", item.Quantidade);
            parameters.Add("FracUN", !string.IsNullOrWhiteSpace(item.Unidade) ? item.Unidade : "UN");
            parameters.Add("FracQtd", item.Quantidade);
            parameters.Add("FracQtdTotal", item.Quantidade);
            parameters.Add("FracPreco", 0m);
            parameters.Add("QtdAtacado", 1m);
            parameters.Add("Peso", item.Peso);
            parameters.Add("Servico", false);
            parameters.Add("Cor", string.Empty);
            parameters.Add("Localizacao", string.Empty);
            parameters.Add("DescRateio", 0m);
            parameters.Add("AcrescRateio", 0m);
            parameters.Add("TotalLiquido", 0m);
            parameters.Add("QtdDevolvida", 0m);
            parameters.Add("LkPreco", 1);
            parameters.Add("LkGuiche", 0);
            parameters.Add("LkBaixa", 0);
            parameters.Add("LkVarejo_Atacado", 0);
            parameters.Add("Entrega", item.Entrega.HasValue ? item.Entrega.Value : (item.IsEntrega ? 1 : 0));

            await conn.ExecuteAsync(sqlInsertB, parameters, transaction: trans);
        }
    }

    public async Task ExcluirItensPorPedidoAsync(int lkEst3A, int? pedidoNum, IDbConnection conn, IDbTransaction trans)
    {
        const string sqlDelete = @"
            DELETE FROM TabEst3B 
            WHERE LkEst3A = @lkEst3A 
               OR (@pedidoNum IS NOT NULL AND @pedidoNum > 0 AND PEDIDO = @pedidoNum)";
        await conn.ExecuteAsync(sqlDelete, new { lkEst3A, pedidoNum }, transaction: trans);
    }

    public Task ExcluirItensPorPedidoAsync(int lkEst3A, IDbConnection conn, IDbTransaction trans)
        => ExcluirItensPorPedidoAsync(lkEst3A, null, conn, trans);
}
