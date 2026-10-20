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
            WITH UsuariosCTE AS (
                SELECT 
                    s.CONTROLE as Controle,
                    s.USUARIO as Nome,
                    s.CODSENHA as CodSenha,
                    CASE 
                        WHEN s.NIVEL IS NOT NULL AND s.NIVEL NOT LIKE '%[^0-9]%' AND LTRIM(RTRIM(s.NIVEL)) <> '' 
                        THEN CAST(s.NIVEL AS int) 
                        ELSE 0 
                    END as Nivel,
                    s.LkVendedor as LkVendedor,
                    s.Inativo as Inativo
                FROM serv s
                WHERE s.USUARIO IS NOT NULL AND LTRIM(RTRIM(s.USUARIO)) <> ''
            )
            SELECT 
                u.Controle,
                u.Nome,
                u.CodSenha,
                u.Nivel,
                u.LkVendedor,
                v.NOME as NomeVendedor,
                v.Celular as CelularVendedor,
                v.Telefone as TelefoneVendedor,
                CAST(CASE 
                    WHEN u.Nivel = 0 THEN ISNULL(lim.OperadorDescVen, 0)
                    WHEN u.Nivel = 1 THEN ISNULL(lim.SupervisorDescVen, 0)
                    WHEN u.Nivel >= 2 THEN ISNULL(lim.GerenteDescVen, 0)
                    ELSE 0 
                END AS decimal(12,2)) as LimiteDesconto
            FROM UsuariosCTE u
            LEFT JOIN TabVend v ON u.LkVendedor = v.CONTROLE
            OUTER APPLY (SELECT TOP 1 OperadorDescVen, SupervisorDescVen, GerenteDescVen FROM ServNivelLimite ORDER BY Controle DESC) lim
            WHERE ISNULL(u.Inativo, 0) = 0
            ORDER BY u.Nome";

        var list = await conn.QueryAsync<Usuario>(sql);
        return list.ToList();
    }

    public async Task<Usuario?> AutenticarAsync(string nomeUsuario, string senha)
    {
        if (string.IsNullOrWhiteSpace(nomeUsuario))
            throw new ArgumentException("O usuário deve ser informado.", nameof(nomeUsuario));

        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            WITH UsuariosCTE AS (
                SELECT 
                    s.CONTROLE as Controle,
                    s.USUARIO as Nome,
                    s.CODSENHA as CodSenha,
                    CASE 
                        WHEN s.NIVEL IS NOT NULL AND s.NIVEL NOT LIKE '%[^0-9]%' AND LTRIM(RTRIM(s.NIVEL)) <> '' 
                        THEN CAST(s.NIVEL AS int) 
                        ELSE 0 
                    END as Nivel,
                    s.LkVendedor as LkVendedor,
                    s.Inativo as Inativo
                FROM serv s
                WHERE UPPER(LTRIM(RTRIM(s.USUARIO))) = UPPER(LTRIM(RTRIM(@usuario)))
            )
            SELECT TOP 1
                u.Controle,
                u.Nome,
                u.CodSenha,
                u.Nivel,
                u.LkVendedor,
                v.NOME as NomeVendedor,
                v.Celular as CelularVendedor,
                v.Telefone as TelefoneVendedor,
                CAST(CASE 
                    WHEN u.Nivel = 0 THEN ISNULL(lim.OperadorDescVen, 0)
                    WHEN u.Nivel = 1 THEN ISNULL(lim.SupervisorDescVen, 0)
                    WHEN u.Nivel >= 2 THEN ISNULL(lim.GerenteDescVen, 0)
                    ELSE 0 
                END AS decimal(12,2)) as LimiteDesconto
            FROM UsuariosCTE u
            LEFT JOIN TabVend v ON u.LkVendedor = v.CONTROLE
            OUTER APPLY (SELECT TOP 1 OperadorDescVen, SupervisorDescVen, GerenteDescVen FROM ServNivelLimite ORDER BY Controle DESC) lim
            WHERE ISNULL(u.Inativo, 0) = 0";

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
            WITH UsuariosCTE AS (
                SELECT 
                    s.CONTROLE as Controle,
                    s.USUARIO as Nome,
                    s.CODSENHA as CodSenha,
                    CASE 
                        WHEN s.NIVEL IS NOT NULL AND s.NIVEL NOT LIKE '%[^0-9]%' AND LTRIM(RTRIM(s.NIVEL)) <> '' 
                        THEN CAST(s.NIVEL AS int) 
                        ELSE 0 
                    END as Nivel,
                    s.LkVendedor as LkVendedor,
                    s.Inativo as Inativo
                FROM serv s
                WHERE s.USUARIO IS NOT NULL AND LTRIM(RTRIM(s.USUARIO)) <> ''
            )
            SELECT 
                u.Controle,
                u.Nome,
                u.CodSenha,
                u.Nivel,
                u.LkVendedor,
                v.NOME as NomeVendedor,
                v.Celular as CelularVendedor,
                v.Telefone as TelefoneVendedor,
                CAST(CASE 
                    WHEN u.Nivel = 0 THEN ISNULL(lim.OperadorDescVen, 0)
                    WHEN u.Nivel = 1 THEN ISNULL(lim.SupervisorDescVen, 0)
                    WHEN u.Nivel >= 2 THEN ISNULL(lim.GerenteDescVen, 0)
                    ELSE 0 
                END AS decimal(12,2)) as LimiteDesconto
            FROM UsuariosCTE u
            LEFT JOIN TabVend v ON u.LkVendedor = v.CONTROLE
            OUTER APPLY (SELECT TOP 1 OperadorDescVen, SupervisorDescVen, GerenteDescVen FROM ServNivelLimite ORDER BY Controle DESC) lim
            WHERE ISNULL(u.Inativo, 0) = 0
              AND u.Nivel >= 1
            ORDER BY u.Nivel DESC, u.Nome ASC";

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
            WITH UsuariosCTE AS (
                SELECT 
                    s.CONTROLE as Controle,
                    s.USUARIO as Nome,
                    s.CODSENHA as CodSenha,
                    CASE 
                        WHEN s.NIVEL IS NOT NULL AND s.NIVEL NOT LIKE '%[^0-9]%' AND LTRIM(RTRIM(s.NIVEL)) <> '' 
                        THEN CAST(s.NIVEL AS int) 
                        ELSE 0 
                    END as Nivel,
                    s.LkVendedor as LkVendedor,
                    s.Inativo as Inativo
                FROM serv s
                WHERE s.CONTROLE = @controle
            )
            SELECT TOP 1
                u.Controle,
                u.Nome,
                u.CodSenha,
                u.Nivel,
                u.LkVendedor,
                v.NOME as NomeVendedor,
                v.Celular as CelularVendedor,
                v.Telefone as TelefoneVendedor,
                CAST(CASE 
                    WHEN u.Nivel = 0 THEN ISNULL(lim.OperadorDescVen, 0)
                    WHEN u.Nivel = 1 THEN ISNULL(lim.SupervisorDescVen, 0)
                    WHEN u.Nivel >= 2 THEN ISNULL(lim.GerenteDescVen, 0)
                    ELSE 0 
                END AS decimal(12,2)) as LimiteDesconto
            FROM UsuariosCTE u
            LEFT JOIN TabVend v ON u.LkVendedor = v.CONTROLE
            OUTER APPLY (SELECT TOP 1 OperadorDescVen, SupervisorDescVen, GerenteDescVen FROM ServNivelLimite ORDER BY Controle DESC) lim
            WHERE ISNULL(u.Inativo, 0) = 0";

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
