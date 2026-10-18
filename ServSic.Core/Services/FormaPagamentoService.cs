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
/// Serviço responsável por carregar as formas de pagamento ativas no sistema (TABEST7)
/// com integração ao plano de contas contábil (TabCx2).
/// </summary>
public class FormaPagamentoService
{
    public async Task<IReadOnlyList<FormaPagamentoTipo>> ListarFormasVendaAsync()
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                t7.Controle,
                t7.Recebimento,
                t7.ContaRec,
                cx2.Controle as LkConta,
                cx2.DESCRICAO as PlanoDescricao,
                CAST(ISNULL(t7.GERACONTA, 0) AS BIT) as GeraConta,
                CAST(ISNULL(t7.ATIVATEF, 0) AS BIT) as AtivaTef,
                CAST(ISNULL(t7.ATIVATROCO, 0) AS BIT) as AtivaTroco,
                t7.TipoPagNFe
            FROM TABEST7 t7
            LEFT JOIN TabCx2 cx2 ON cx2.CODIGO = t7.ContaRec
            WHERE ISNULL(t7.deletado, 0) = 0 
              AND ISNULL(t7.HabVenda, 1) = 1
            ORDER BY 
                CASE 
                    WHEN t7.Recebimento LIKE '%DINHEIRO%' THEN 1
                    WHEN t7.Recebimento LIKE '%PIX%' THEN 2
                    WHEN t7.Recebimento LIKE '%DEBITO%' THEN 3
                    WHEN t7.Recebimento LIKE '%CREDITO%' THEN 4
                    WHEN t7.Recebimento LIKE '%CARNE%' OR t7.Recebimento LIKE '%DUPLICATA%' THEN 5
                    ELSE 10
                END,
                t7.Controle";

        var formas = (await conn.QueryAsync<FormaPagamentoTipo>(sql)).ToList();

        // Atribui atalhos numéricos 1..9 para acesso rápido pelo teclado
        int atalho = 1;
        foreach (var f in formas)
        {
            if (atalho <= 9)
            {
                f.AtalhoNumero = atalho++;
            }
        }

        return formas;
    }

    public Task<IReadOnlyList<FormaPagamentoTipo>> ListarAtivasAsync() => ListarFormasVendaAsync();
}
