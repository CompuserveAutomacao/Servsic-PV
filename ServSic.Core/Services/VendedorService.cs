using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

public class VendedorService
{
    public async Task<IReadOnlyList<Vendedor>> ListarAsync()
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                CONTROLE as Controle,
                NOME as Nome
            FROM TabVend
            WHERE NOME IS NOT NULL AND LTRIM(RTRIM(NOME)) <> ''
            ORDER BY NOME";

        var list = await conn.QueryAsync<Vendedor>(sql);
        return list.ToList();
    }
}
