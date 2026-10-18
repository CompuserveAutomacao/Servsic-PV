using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

public class ProdutoService
{
    public async Task<IReadOnlyList<Produto>> BuscarAsync(string termo, int limite = 50)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        string whereClause;
        var parameters = new DynamicParameters();
        parameters.Add("limite", limite);

        if (string.IsNullOrWhiteSpace(termo))
        {
            whereClause = "WHERE (Inativo IS NULL OR Inativo = 'N' OR Inativo = '0')";
        }
        else
        {
            parameters.Add("likeTerm", $"%{termo.Trim()}%");
            parameters.Add("exactTerm", termo.Trim());

            whereClause = @"
                WHERE (e0.Inativo IS NULL OR e0.Inativo = 'N' OR e0.Inativo = '0')
                  AND (e0.CodBarra = @exactTerm OR e0.CodInterno = @exactTerm OR e0.CodInterno LIKE @likeTerm OR e0.Produto LIKE @likeTerm OR e0.Fabricante LIKE @likeTerm)";
        }

        string sql = $@"
            SELECT TOP (@limite)
                e0.Controle,
                e0.CodInterno,
                e0.Produto as Descricao,
                e0.CodBarra,
                e0.Fabricante,
                e0.Unidade,
                e0.PrVenda,
                e0.PrCusto,
                e0.Estoque,
                e0.LkSetor,
                ISNULL(e1.PerDescMax, 0) as PerDescMax
            FROM TabEst0 e0
            LEFT JOIN TabEst1 e1 ON e1.Controle = e0.Controle
            {whereClause}
            ORDER BY e0.Produto";

        var list = await conn.QueryAsync<Produto>(sql, parameters);
        return list.ToList();
    }

    public async Task<Produto?> ObterPorIdAsync(int controle)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP 1
                e0.Controle,
                e0.CodInterno,
                e0.Produto as Descricao,
                e0.CodBarra,
                e0.Fabricante,
                e0.Unidade,
                e0.PrVenda,
                e0.PrCusto,
                e0.Estoque,
                e0.LkSetor,
                ISNULL(e1.PerDescMax, 0) as PerDescMax
            FROM TabEst0 e0
            LEFT JOIN TabEst1 e1 ON e1.Controle = e0.Controle
            WHERE e0.Controle = @controle";

        return await conn.QueryFirstOrDefaultAsync<Produto>(sql, new { controle });
    }

    public async Task<Produto?> ObterPorCodigoBarraOuInternoAsync(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP 1
                e0.Controle,
                e0.CodInterno,
                e0.Produto as Descricao,
                e0.CodBarra,
                e0.Fabricante,
                e0.Unidade,
                e0.PrVenda,
                e0.PrCusto,
                e0.Estoque,
                e0.LkSetor,
                ISNULL(e1.PerDescMax, 0) as PerDescMax
            FROM TabEst0 e0
            LEFT JOIN TabEst1 e1 ON e1.Controle = e0.Controle
            WHERE (e0.CodBarra = @codigo OR e0.CodInterno = @codigo)
              AND (e0.Inativo IS NULL OR e0.Inativo = 'N' OR e0.Inativo = '0')";

        return await conn.QueryFirstOrDefaultAsync<Produto>(sql, new { codigo = codigo.Trim() });
    }
}
