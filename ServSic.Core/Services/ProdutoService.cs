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
    private const string BaseSelect = @"
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
            ISNULL(e.Inativo, 0) as Inativo,
            img.Imagem as FotoBytes,
            img.pathArquivo as FotoPath
        FROM TabEst1 e
        LEFT JOIN TabEst8 grp ON grp.Controle = e.LkSetor
        LEFT JOIN TabEst8b sub ON sub.Controle = e.LkSubGrupo
        LEFT JOIN TabEst12 fab ON fab.Controle = e.LkFabricante
        OUTER APPLY (
            SELECT TOP 1 Imagem, pathArquivo 
            FROM TabEst1Img 
            WHERE LkCadastro = e.Controle
            ORDER BY idControle ASC
        ) img";

    public async Task<IReadOnlyList<Produto>> BuscarAsync(string termo, int limite = 100)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("limite", limite);

        var sbWhere = new StringBuilder();
        sbWhere.Append("WHERE (e.Inativo IS NULL OR e.Inativo = 0)");

        if (!string.IsNullOrWhiteSpace(termo))
        {
            string termoLimpo = termo.Trim();
            var tokens = termoLimpo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length == 1)
            {
                bool isNumeric = int.TryParse(termoLimpo, out int controleNum);
                parameters.Add("exactTerm", termoLimpo);
                parameters.Add("likeTerm", $"%{termoLimpo}%");
                parameters.Add("controleNum", isNumeric ? controleNum : -1);

                sbWhere.Append(@"
                    AND (
                        e.Controle = @controleNum
                        OR e.CodBarra = @exactTerm 
                        OR e.CodBarraCx = @exactTerm
                        OR e.CodInterno = @exactTerm 
                        OR e.Codigo = @exactTerm
                        OR e.CodORIGEM = @exactTerm
                        OR e.CodFornecedor = @exactTerm
                        OR e.NCM = @exactTerm
                        OR e.CEST = @exactTerm
                        OR e.Produto LIKE @likeTerm 
                        OR e.DESCRICAO LIKE @likeTerm
                        OR e.Fabricante LIKE @likeTerm
                        OR fab.Nome LIKE @likeTerm
                        OR grp.Setor LIKE @likeTerm
                        OR sub.Descricao LIKE @likeTerm
                        OR e.Moeda LIKE @likeTerm
                        OR e.CodBarra LIKE @likeTerm
                        OR e.CodBarraCx LIKE @likeTerm
                        OR e.CodInterno LIKE @likeTerm
                        OR e.Codigo LIKE @likeTerm
                        OR e.CodORIGEM LIKE @likeTerm
                        OR e.CodFornecedor LIKE @likeTerm
                        OR e.NCM LIKE @likeTerm
                    )");
            }
            else
            {
                // Pesquisa inteligente multi-termo (combina palavras entre descrição, códigos, marca, grupo, etc.)
                for (int i = 0; i < tokens.Length; i++)
                {
                    string paramName = $"token_{i}";
                    parameters.Add(paramName, $"%{tokens[i]}%");

                    sbWhere.Append($@"
                        AND (
                            e.Produto LIKE @{paramName}
                            OR e.DESCRICAO LIKE @{paramName}
                            OR e.Fabricante LIKE @{paramName}
                            OR fab.Nome LIKE @{paramName}
                            OR grp.Setor LIKE @{paramName}
                            OR sub.Descricao LIKE @{paramName}
                            OR e.Moeda LIKE @{paramName}
                            OR e.CodInterno LIKE @{paramName}
                            OR e.Codigo LIKE @{paramName}
                            OR e.CodBarra LIKE @{paramName}
                            OR e.CodBarraCx LIKE @{paramName}
                            OR e.CodORIGEM LIKE @{paramName}
                            OR e.CodFornecedor LIKE @{paramName}
                            OR e.NCM LIKE @{paramName}
                            OR e.CEST LIKE @{paramName}
                        )");
                }
            }
        }

        string sql = string.Format(BaseSelect, "TOP (@limite)") + "\n" + sbWhere + "\nORDER BY e.Produto";
        var list = await conn.QueryAsync<Produto>(sql, parameters);
        return list.ToList();
    }

    public async Task<Produto?> ObterPorIdAsync(int controle)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        string sql = string.Format(BaseSelect, "TOP 1") + " WHERE e.Controle = @controle";
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

        string sql = string.Format(BaseSelect, "TOP 1") + @"
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
}
