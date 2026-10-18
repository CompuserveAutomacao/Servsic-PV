using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

public class ProdutoService
{
    // Consulta Leve e Ultra Rápida: Apenas TabEst1 direta, ZERO LEFT JOINs e apenas produtos ativos
    private const string SelectCard = @"
        SELECT {0}
            e.Controle,
            e.Codigo,
            e.CodInterno,
            e.CodBarra,
            e.CodBarraCx,
            e.CodORIGEM as CodOrigem,
            e.CodFornecedor,
            e.Produto as Descricao,
            ISNULL(e.Unidade, 'UN') as Unidade,
            ISNULL(e.PrecoVenda, 0) as PrVenda,
            ISNULL(e.Quantidade, 0) as EstoqueLoja,
            ISNULL(e.QtdDepos, 0) as EstoqueDeposito,
            (ISNULL(e.Quantidade, 0) + ISNULL(e.QtdDepos, 0)) as Estoque,
            ISNULL(e.PerDescMax, 0) as PerDescMax,
            ISNULL(e.Inativo, 0) as Inativo
        FROM TabEst1 e";

    // Consulta Completa: Carregada exclusivamente sob demanda ao selecionar/clicar no produto para a Ficha Técnica
    private const string SelectCompleto = @"
        SELECT {0}
            e.Controle,
            e.Codigo,
            e.CodInterno,
            e.CodBarra,
            e.CodBarraCx,
            e.CodORIGEM as CodOrigem,
            e.CodFornecedor,
            e.NCM,
            e.CEST,
            ISNULL(e.Apresenta, '') as Apresentacao,
            e.Produto as Descricao,
            e.DESCRICAO as DescricaoDetalhada,
            COALESCE(NULLIF(LTRIM(RTRIM(e.Fabricante)), ''), fab.Nome, '') as Fabricante,
            ISNULL(e.Unidade, 'UN') as Unidade,
            ISNULL(e.PrecoVenda, 0) as PrVenda,
            ISNULL(e.PrPrazo, 0) as PrPrazo,
            ISNULL(e.PrAtacado, 0) as PrAtacado,
            ISNULL(e.PRMINIMO, 0) as PrMinimo,
            ISNULL(e.PrAtacado1, 0) as PrAtacado1,
            ISNULL(e.QtdAtacado, 0) as QtdAtacado,
            ISNULL(e.UnAtacado, '') as UnAtacado,
            ISNULL(e.PrecoCusto, 0) as PrCusto,
            ISNULL(e.Quantidade, 0) as EstoqueLoja,
            ISNULL(e.QtdDepos, 0) as EstoqueDeposito,
            (ISNULL(e.Quantidade, 0) + ISNULL(e.QtdDepos, 0)) as Estoque,
            ISNULL(e.EstMinimo, 0) as EstMinimo,
            ISNULL(e.Moeda, '') as Localizacao,
            e.LkSetor,
            ISNULL(grp.Setor, '') as Grupo,
            e.LkSubGrupo,
            ISNULL(sub.Descricao, '') as SubGrupo,
            ISNULL(e.PerDescMax, 0) as PerDescMax,
            ISNULL(e.Inativo, 0) as Inativo
        FROM TabEst1 e
        LEFT JOIN TabEst8 grp ON grp.Controle = e.LkSetor
        LEFT JOIN TabEst8b sub ON sub.Controle = e.LkSubGrupo
        LEFT JOIN TabEst12 fab ON fab.Controle = e.LkFabricante";

    public Task<IReadOnlyList<Produto>> BuscarAsync(
        string? termo, 
        int limite, 
        CancellationToken cancellationToken = default)
    {
        return BuscarAsync(termo, marca: null, grupo: null, localizacao: null, limite: limite, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Produto>> BuscarAsync(
        string? termo = null, 
        string? marca = null, 
        string? grupo = null, 
        string? localizacao = null, 
        int limite = 100, 
        CancellationToken cancellationToken = default)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("limite", limite);

        var sbWhere = new StringBuilder();
        sbWhere.Append("WHERE (e.Inativo IS NULL OR e.Inativo = 0)");
        string orderBy = "ORDER BY e.Produto";

        // 1. Barra de pesquisa principal: Consulta por TODOS os códigos e descrição do produto
        if (!string.IsNullOrWhiteSpace(termo))
        {
            string termoLimpo = termo.Trim();
            var tokens = termoLimpo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length == 1)
            {
                bool isNumeric = int.TryParse(termoLimpo, out int controleNum);
                parameters.Add("exactTerm", termoLimpo);
                parameters.Add("likeTerm", $"%{termoLimpo}%");
                parameters.Add("startsTerm", $"{termoLimpo}%");
                parameters.Add("controleNum", isNumeric ? controleNum : -1);

                sbWhere.Append(@"
                    AND (
                        e.Controle = @controleNum
                        OR e.CodBarra = @exactTerm 
                        OR e.CodInterno = @exactTerm 
                        OR e.Codigo = @exactTerm
                        OR e.CodORIGEM = @exactTerm
                        OR e.CodBarraCx = @exactTerm
                        OR e.CodFornecedor = @exactTerm
                        OR e.CodBarra LIKE @startsTerm
                        OR e.CodInterno LIKE @startsTerm
                        OR e.Produto LIKE @likeTerm 
                    )");

                orderBy = @"
                    ORDER BY 
                        CASE 
                            WHEN e.Controle = @controleNum THEN 0
                            WHEN e.CodBarra = @exactTerm OR e.CodInterno = @exactTerm THEN 1
                            WHEN e.Codigo = @exactTerm OR e.CodORIGEM = @exactTerm THEN 2
                            WHEN e.CodBarraCx = @exactTerm OR e.CodFornecedor = @exactTerm THEN 3
                            WHEN e.Produto LIKE @startsTerm THEN 4
                            ELSE 5
                        END,
                        e.Produto ASC";
            }
            else
            {
                // Pesquisa multi-termo ultra rápida: palavras na Descrição e códigos exatos
                for (int i = 0; i < tokens.Length; i++)
                {
                    string paramName = $"token_{i}";
                    string tokenText = tokens[i];
                    bool tokenIsNum = int.TryParse(tokenText, out int tokenNum);

                    parameters.Add(paramName, $"%{tokenText}%");
                    parameters.Add($"{paramName}_num", tokenIsNum ? tokenNum : -1);
                    parameters.Add($"{paramName}_exact", tokenText);

                    sbWhere.Append($@"
                        AND (
                            e.Produto LIKE @{paramName}
                            OR e.Controle = @{paramName}_num
                            OR e.CodBarra = @{paramName}_exact
                            OR e.CodInterno = @{paramName}_exact
                            OR e.Codigo = @{paramName}_exact
                            OR e.CodORIGEM = @{paramName}_exact
                        )");
                }

                parameters.Add("firstTokenLike", $"{tokens[0]}%");
                orderBy = @"
                    ORDER BY 
                        CASE 
                            WHEN e.Produto LIKE @firstTokenLike THEN 1
                            ELSE 2
                        END,
                        e.Produto ASC";
            }
        }

        // 2. Filtro dedicado: Marca / Fabricante (direto em TabEst1, sem joins)
        if (!string.IsNullOrWhiteSpace(marca))
        {
            parameters.Add("marca", $"%{marca.Trim()}%");
            sbWhere.Append(" AND e.Fabricante LIKE @marca");
        }

        // 3. Filtro dedicado: Grupo / Setor (subconsulta leve, sem joins no select principal)
        if (!string.IsNullOrWhiteSpace(grupo))
        {
            parameters.Add("grupo", $"%{grupo.Trim()}%");
            sbWhere.Append(" AND EXISTS (SELECT 1 FROM TabEst8 grp WHERE grp.Controle = e.LkSetor AND grp.Setor LIKE @grupo)");
        }

        // 4. Filtro dedicado: Localização no Estoque (Moeda)
        if (!string.IsNullOrWhiteSpace(localizacao))
        {
            parameters.Add("localizacao", $"%{localizacao.Trim()}%");
            sbWhere.Append(" AND e.Moeda LIKE @localizacao");
        }

        string sql = string.Format(SelectCard, "TOP (@limite)") + "\n" + sbWhere + "\n" + orderBy;
        var cmd = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        var list = await conn.QueryAsync<Produto>(cmd);
        return list.ToList();
    }

    public async Task<(byte[]? Bytes, string? Path)> ObterInfoFotoAsync(int controle)
    {
        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT TOP 1 Imagem, pathArquivo FROM TabEst1Img WHERE LkCadastro = @controle ORDER BY idControle ASC",
                new { controle });
            if (row == null) return (null, null);
            byte[]? b = row.Imagem as byte[];
            string? p = row.pathArquivo as string;
            return (b, p);
        }
        catch
        {
            return (null, null);
        }
    }

    public async Task<byte[]?> ObterFotoBytesAsync(int controle)
    {
        var (bytes, _) = await ObterInfoFotoAsync(controle);
        return bytes;
    }

    public async Task<Produto?> ObterPorIdAsync(int controle)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        string sql = string.Format(SelectCompleto, "TOP 1") + " WHERE e.Controle = @controle";
        return await conn.QueryFirstOrDefaultAsync<Produto>(sql, new { controle });
    }

    public async Task<Produto?> ObterPorCodigoBarraOuInternoAsync(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        using var conn = DbConnectionFactory.CreateConnection();
        string codigoLimpo = codigo.Trim();
        bool isNumeric = int.TryParse(codigoLimpo, out int codInt);

        var parameters = new DynamicParameters();
        parameters.Add("codigo", codigoLimpo);
        parameters.Add("codInt", isNumeric ? codInt : -1);

        string sql = string.Format(SelectCompleto, "TOP 1") + @"
            WHERE (e.Inativo IS NULL OR e.Inativo = 0)
              AND (
                  e.Controle = @codInt
                  OR e.CodBarra = @codigo 
                  OR e.CodBarraCx = @codigo 
                  OR e.CodInterno = @codigo 
                  OR e.Codigo = @codigo
                  OR e.CodORIGEM = @codigo
                  OR e.CodFornecedor = @codigo
                  OR e.NCM = @codigo
                  OR e.CEST = @codigo
              )";

        return await conn.QueryFirstOrDefaultAsync<Produto>(sql, parameters);
    }

    public async Task<IReadOnlyList<string>> ObterMarcasDisponiveisAsync()
    {
        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            const string sql = @"
                SELECT DISTINCT LTRIM(RTRIM(Nome)) as Nome 
                FROM (
                    SELECT Nome FROM TabEst12 WHERE Nome IS NOT NULL AND LTRIM(RTRIM(Nome)) <> ''
                    UNION
                    SELECT Fabricante as Nome FROM TabEst1 WHERE Fabricante IS NOT NULL AND LTRIM(RTRIM(Fabricante)) <> ''
                ) f
                ORDER BY 1";
            var result = await conn.QueryAsync<string>(sql);
            return result.ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<string>> ObterGruposDisponiveisAsync()
    {
        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            const string sql = @"
                SELECT DISTINCT LTRIM(RTRIM(Setor)) as Setor 
                FROM TabEst8 
                WHERE Setor IS NOT NULL AND LTRIM(RTRIM(Setor)) <> '' 
                ORDER BY 1";
            var result = await conn.QueryAsync<string>(sql);
            return result.ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<string>> ObterLocalizacoesDisponiveisAsync()
    {
        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            const string sql = @"
                SELECT DISTINCT LTRIM(RTRIM(Moeda)) as Loc 
                FROM TabEst1 
                WHERE Moeda IS NOT NULL AND LTRIM(RTRIM(Moeda)) <> '' 
                ORDER BY 1";
            var result = await conn.QueryAsync<string>(sql);
            return result.ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
