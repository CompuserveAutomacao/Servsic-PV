using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

public class ClienteService
{
    public async Task<IReadOnlyList<Cliente>> BuscarAsync(string termo, int limite = 50)
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
            termo = termo.Trim();
            bool isNumeric = int.TryParse(termo, out _);

            if (isNumeric)
            {
                parameters.Add("codigo", termo);
                parameters.Add("codigoPad6", termo.PadLeft(6, '0'));
                parameters.Add("codigoPad5", termo.PadLeft(5, '0'));
                parameters.Add("codigoPad4", termo.PadLeft(4, '0'));
                parameters.Add("likeTerm", $"%{termo}%");

                whereClause = @"
                    WHERE (Inativo IS NULL OR Inativo = 'N' OR Inativo = '0')
                      AND (Codigo = @codigo OR Codigo = @codigoPad6 OR Codigo = @codigoPad5 OR Codigo = @codigoPad4 OR Nome LIKE @likeTerm OR Cpf LIKE @likeTerm OR CGC LIKE @likeTerm OR Telefone LIKE @likeTerm)";
            }
            else
            {
                parameters.Add("likeTerm", $"%{termo}%");
                whereClause = @"
                    WHERE (Inativo IS NULL OR Inativo = 'N' OR Inativo = '0')
                      AND (Nome LIKE @likeTerm OR Cpf LIKE @likeTerm OR CGC LIKE @likeTerm OR Telefone LIKE @likeTerm OR Celular LIKE @likeTerm)";
            }
        }

        string sql = $@"
            SELECT TOP (@limite)
                Controle,
                Codigo,
                Nome,
                Cpf,
                CGC,
                Endereco,
                Numero,
                Bairro,
                Cidade,
                Estado,
                CEP,
                Telefone,
                Celular,
                eMail as Email
            FROM TABCLI
            {whereClause}
            ORDER BY Nome";

        var list = await conn.QueryAsync<Cliente>(sql, parameters);
        return list.ToList();
    }

    public async Task<Cliente?> ObterPorIdAsync(int controle)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP 1
                Controle,
                Codigo,
                Nome,
                Cpf,
                CGC,
                Endereco,
                Numero,
                Bairro,
                Cidade,
                Estado,
                CEP,
                Telefone,
                Celular,
                eMail as Email
            FROM TABCLI
            WHERE Controle = @controle";

        return await conn.QueryFirstOrDefaultAsync<Cliente>(sql, new { controle });
    }

    public async Task<Cliente> CadastrarRapidoAsync(Cliente cliente)
    {
        if (string.IsNullOrWhiteSpace(cliente.Nome))
            throw new System.ArgumentException("O nome do cliente é obrigatório.", nameof(cliente.Nome));

        using var conn = DbConnectionFactory.CreateConnection();
        conn.Open();

        const string sqlNewCod = @"
            DECLARE @NewCod INT;
            BEGIN TRY
                EXEC NewCod_TabCli @NewCod OUTPUT;
            END TRY
            BEGIN CATCH
                SELECT @NewCod = ISNULL(MAX(Controle), 0) + 1 FROM TabCli;
            END CATCH
            SELECT @NewCod;";

        int novoControle = await conn.ExecuteScalarAsync<int>(sqlNewCod);
        cliente.Controle = novoControle;
        cliente.Codigo = novoControle.ToString().PadLeft(6, '0');

        string doc = cliente.Documento?.Trim() ?? string.Empty;
        bool isCnpj = doc.Length > 11;

        string? cpf = !isCnpj ? doc : cliente.Cpf;
        string? cgc = isCnpj ? doc : cliente.CGC;

        const string sqlInsert = @"
            INSERT INTO TabCli (
                Controle, Codigo, Nome, FANTASIA_APELIDO, Cpf, CGC,
                Telefone, Celular, Endereco, Numero, Bairro, Cidade, Estado, Cep, eMail,
                Data, Inativo
            ) VALUES (
                @Controle, @Codigo, @Nome, @Fantasia, @Cpf, @CGC,
                @Telefone, @Celular, @Endereco, @Numero, @Bairro, @Cidade, @Estado, @CEP, @Email,
                @Data, 'N'
            );";

        await conn.ExecuteAsync(sqlInsert, new
        {
            Controle = cliente.Controle,
            Codigo = cliente.Codigo,
            Nome = cliente.Nome.Trim(),
            Fantasia = cliente.Fantasia?.Trim() ?? cliente.Nome.Trim(),
            Cpf = cpf,
            CGC = cgc,
            Telefone = cliente.Telefone?.Trim(),
            Celular = cliente.Celular?.Trim(),
            Endereco = cliente.Endereco?.Trim(),
            Numero = cliente.Numero?.Trim(),
            Bairro = cliente.Bairro?.Trim(),
            Cidade = cliente.Cidade?.Trim(),
            Estado = cliente.Estado?.Trim(),
            CEP = cliente.CEP?.Trim(),
            Email = cliente.Email?.Trim(),
            Data = System.DateTime.Now
        });

        return cliente;
    }
}
