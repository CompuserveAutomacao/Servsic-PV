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
/// Serviço de gerenciamento de múltiplos endereços / destinos (obras, filiais, etc.) do cliente.
/// Tabela: dbo.TabCliEndereco.
/// </summary>
public class ClienteEnderecoService
{
    public async Task<IReadOnlyList<ClienteEndereco>> ListarAtivosAsync(int lkCliente, string? filtro = null)
    {
        if (lkCliente <= 0) return Array.Empty<ClienteEndereco>();

        using var conn = DbConnectionFactory.CreateConnection();
        string sql = @"
            SELECT 
                Controle, LkCliente, Apelido, Endereco, Numero, ComplEnd, 
                Bairro, Cidade, Estado, Cep, CodMunicipio, Obs, Inativo, DtUltAtualizacao
            FROM dbo.TabCliEndereco
            WHERE LkCliente = @lkCliente
              AND ISNULL(Inativo, 0) = 0";

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            sql += @" AND (
                Apelido LIKE @f 
                OR Endereco LIKE @f 
                OR Bairro LIKE @f 
                OR Cidade LIKE @f
            )";
        }

        sql += " ORDER BY Apelido";

        var list = await conn.QueryAsync<ClienteEndereco>(sql, new 
        { 
            lkCliente, 
            f = $"%{filtro?.Trim()}%" 
        });

        return list.ToList();
    }

    public async Task<ClienteEndereco?> ObterAsync(int controle)
    {
        if (controle <= 0) return null;

        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                Controle, LkCliente, Apelido, Endereco, Numero, ComplEnd, 
                Bairro, Cidade, Estado, Cep, CodMunicipio, Obs, Inativo, DtUltAtualizacao
            FROM dbo.TabCliEndereco
            WHERE Controle = @controle";

        return await conn.QueryFirstOrDefaultAsync<ClienteEndereco>(sql, new { controle });
    }

    public async Task<int> IncluirAsync(ClienteEndereco novo)
    {
        if (novo == null) throw new ArgumentNullException(nameof(novo));
        if (string.IsNullOrWhiteSpace(novo.Apelido)) throw new InvalidOperationException("O campo Local / Destino (Apelido) é obrigatório.");

        using var conn = DbConnectionFactory.CreateConnection();
        const string sqlInsert = @"
            INSERT INTO dbo.TabCliEndereco (
                LkCliente, Apelido, Endereco, Numero, ComplEnd, 
                Bairro, Cidade, Estado, Cep, CodMunicipio, Obs, Inativo, DtUltAtualizacao
            )
            VALUES (
                @LkCliente, UPPER(@Apelido), UPPER(@Endereco), @Numero, UPPER(@ComplEnd),
                UPPER(@Bairro), UPPER(@Cidade), UPPER(@Estado), @Cep, @CodMunicipio, UPPER(@Obs), 0, GETDATE()
            );
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        int novoControle = await conn.ExecuteScalarAsync<int>(sqlInsert, novo);
        novo.Controle = novoControle;
        return novoControle;
    }

    public async Task<bool> SoftDeleteAsync(int controle, int lkCliente)
    {
        if (controle <= 0) return false;

        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.TabCliEndereco
            SET Inativo = 1, DtUltAtualizacao = GETDATE()
            WHERE Controle = @controle AND LkCliente = @lkCliente";

        int rows = await conn.ExecuteAsync(sql, new { controle, lkCliente });
        return rows > 0;
    }
}
