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
                v.NOME as NomeVendedor
            FROM serv s
            LEFT JOIN TabVend v ON s.LkVendedor = v.CONTROLE
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
                v.NOME as NomeVendedor
            FROM serv s
            LEFT JOIN TabVend v ON s.LkVendedor = v.CONTROLE
            WHERE UPPER(LTRIM(RTRIM(s.USUARIO))) = UPPER(LTRIM(RTRIM(@usuario)))";

        var user = await conn.QueryFirstOrDefaultAsync<Usuario>(sql, new { usuario = nomeUsuario });
        if (user == null)
            return null;

        bool senhaValida = ServPasswordHasher.ValidarSenha(senha, user.CodSenha);
        return senhaValida ? user : null;
    }
}
