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
                tb.Vendedor
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
        IDbConnection conn,
        IDbTransaction trans)
    {
        // 1. Remove itens anteriores para atualização atômica
        await ExcluirItensPorPedidoAsync(lkEst3A, conn, trans);

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
                Controle, LkEst3A, PEDIDO, LkProduto, CodInterno, Produto,
                Quantidade, VlUnitario, Desconto, Acrescimo, Total, PrecoVenda,
                PrCusto, Unidade, MARCA, LkVendedor, Vendedor
            ) VALUES (
                @Controle, @LkEst3A, @Pedido, @LkProduto, @CodInterno, @Produto,
                @Quantidade, @VlUnitario, @Desconto, @Acrescimo, @Total, @PrecoVenda,
                @PrCusto, @Unidade, @Marca, @LkVendedor, @Vendedor
            );";

        foreach (var item in itens)
        {
            item.Controle = await conn.ExecuteScalarAsync<int>(sqlNewCodB, transaction: trans);
            item.LkEst3A = lkEst3A;
            item.Pedido = pedidoNum;
            item.LkVendedor ??= lkVendedor;
            item.Vendedor ??= vendedor;

            await conn.ExecuteAsync(sqlInsertB, item, transaction: trans);
        }
    }

    public async Task ExcluirItensPorPedidoAsync(int lkEst3A, IDbConnection conn, IDbTransaction trans)
    {
        const string sqlDelete = "DELETE FROM TabEst3B WHERE LkEst3A = @lkEst3A";
        await conn.ExecuteAsync(sqlDelete, new { lkEst3A }, transaction: trans);
    }
}
