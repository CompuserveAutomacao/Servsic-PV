using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;
using ServSic.Core.Security;

namespace ServSic.Core.Services;

public class AuthService
{
    public async Task<bool> TestarConexaoAsync()
    {
        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            var result = await conn.ExecuteScalarAsync<int>("SELECT 1");
            return result == 1;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<Usuario>> ListarUsuariosAtivosAsync()
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                s.CONTROLE as Controle,
                s.USUARIO as Nome,
                s.CODSENHA as CodSenha,
                s.NIVEL as Nivel,
                s.LkVendedor as LkVendedor,
                v.NOME as NomeVendedor,
                v.Celular as CelularVendedor,
                v.Telefone as TelefoneVendedor,
                CAST(CASE 
                    WHEN s.NIVEL = 0 THEN ISNULL(lim.OperadorDescVen, 0)
                    WHEN s.NIVEL = 1 THEN ISNULL(lim.SupervisorDescVen, 0)
                    WHEN s.NIVEL >= 2 THEN ISNULL(lim.GerenteDescVen, 0)
                    ELSE 0 
                END AS decimal(12,2)) as LimiteDesconto
            FROM serv s
            LEFT JOIN TabVend v ON s.LkVendedor = v.CONTROLE
            OUTER APPLY (SELECT TOP 1 OperadorDescVen, SupervisorDescVen, GerenteDescVen FROM ServNivelLimite ORDER BY Controle DESC) lim
            WHERE s.USUARIO IS NOT NULL AND LTRIM(RTRIM(s.USUARIO)) <> ''
            ORDER BY s.USUARIO";

        var list = await conn.QueryAsync<Usuario>(sql);
        return list.ToList();
    }

    public async Task<Usuario?> AutenticarAsync(string nomeUsuario, string senha)
    {
        if (string.IsNullOrWhiteSpace(nomeUsuario))
            throw new ArgumentException("O usuário deve ser informado.", nameof(nomeUsuario));

        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP 1
                s.CONTROLE as Controle,
                s.USUARIO as Nome,
                s.CODSENHA as CodSenha,
                s.NIVEL as Nivel,
                s.LkVendedor as LkVendedor,
                v.NOME as NomeVendedor,
                v.Celular as CelularVendedor,
                v.Telefone as TelefoneVendedor,
                CAST(CASE 
                    WHEN s.NIVEL = 0 THEN ISNULL(lim.OperadorDescVen, 0)
                    WHEN s.NIVEL = 1 THEN ISNULL(lim.SupervisorDescVen, 0)
                    WHEN s.NIVEL >= 2 THEN ISNULL(lim.GerenteDescVen, 0)
                    ELSE 0 
                END AS decimal(12,2)) as LimiteDesconto
            FROM serv s
            LEFT JOIN TabVend v ON s.LkVendedor = v.CONTROLE
            OUTER APPLY (SELECT TOP 1 OperadorDescVen, SupervisorDescVen, GerenteDescVen FROM ServNivelLimite ORDER BY Controle DESC) lim
            WHERE UPPER(LTRIM(RTRIM(s.USUARIO))) = UPPER(LTRIM(RTRIM(@usuario)))";

        var user = await conn.QueryFirstOrDefaultAsync<Usuario>(sql, new { usuario = nomeUsuario });
        if (user == null)
            return null;

        bool senhaValida = ServPasswordHasher.ValidarSenha(senha, user.CodSenha);
        return senhaValida ? user : null;
    }

    public async Task<IReadOnlyList<Usuario>> ListarGerentesAtivosAsync()
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                s.CONTROLE as Controle,
                s.USUARIO as Nome,
                s.CODSENHA as CodSenha,
                s.NIVEL as Nivel,
                s.LkVendedor as LkVendedor,
                v.NOME as NomeVendedor,
                v.Celular as CelularVendedor,
                v.Telefone as TelefoneVendedor,
                CAST(CASE 
                    WHEN s.NIVEL = 0 THEN ISNULL(lim.OperadorDescVen, 0)
                    WHEN s.NIVEL = 1 THEN ISNULL(lim.SupervisorDescVen, 0)
                    WHEN s.NIVEL >= 2 THEN ISNULL(lim.GerenteDescVen, 0)
                    ELSE 0 
                END AS decimal(12,2)) as LimiteDesconto
            FROM serv s
            LEFT JOIN TabVend v ON s.LkVendedor = v.CONTROLE
            OUTER APPLY (SELECT TOP 1 OperadorDescVen, SupervisorDescVen, GerenteDescVen FROM ServNivelLimite ORDER BY Controle DESC) lim
            WHERE s.USUARIO IS NOT NULL 
              AND LTRIM(RTRIM(s.USUARIO)) <> ''
              AND s.NIVEL >= 1
            ORDER BY s.NIVEL DESC, s.USUARIO ASC";

        var list = await conn.QueryAsync<Usuario>(sql);
        return list.ToList();
    }

    public async Task<(bool Sucesso, string Mensagem, Usuario? Gerente)> ValidarLiberacaoGerenteAsync(int controleGerente, string senha)
    {
        if (controleGerente <= 0)
            return (false, "Selecione o gerente responsável pela liberação.", null);

        if (string.IsNullOrEmpty(senha))
            return (false, "Informe a senha do gerente.", null);

        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP 1
                s.CONTROLE as Controle,
                s.USUARIO as Nome,
                s.CODSENHA as CodSenha,
                s.NIVEL as Nivel,
                s.LkVendedor as LkVendedor,
                v.NOME as NomeVendedor,
                v.Celular as CelularVendedor,
                v.Telefone as TelefoneVendedor,
                CAST(CASE 
                    WHEN s.NIVEL = 0 THEN ISNULL(lim.OperadorDescVen, 0)
                    WHEN s.NIVEL = 1 THEN ISNULL(lim.SupervisorDescVen, 0)
                    WHEN s.NIVEL >= 2 THEN ISNULL(lim.GerenteDescVen, 0)
                    ELSE 0 
                END AS decimal(12,2)) as LimiteDesconto
            FROM serv s
            LEFT JOIN TabVend v ON s.LkVendedor = v.CONTROLE
            OUTER APPLY (SELECT TOP 1 OperadorDescVen, SupervisorDescVen, GerenteDescVen FROM ServNivelLimite ORDER BY Controle DESC) lim
            WHERE s.CONTROLE = @controle";

        var user = await conn.QueryFirstOrDefaultAsync<Usuario>(sql, new { controle = controleGerente });
        if (user == null)
            return (false, "Usuário gerente não localizado no sistema.", null);

        if (user.Nivel < 1)
            return (false, "O usuário selecionado não possui permissão de supervisor ou gerente.", null);

        bool senhaValida = ServPasswordHasher.ValidarSenha(senha, user.CodSenha);
        if (!senhaValida)
            return (false, "Senha de gerente incorreta.", null);

        return (true, "Liberação autorizada com sucesso.", user);
    }
}
