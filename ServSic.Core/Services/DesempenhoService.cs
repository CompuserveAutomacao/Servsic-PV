using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço responsável pelo cálculo e fornecimento dos dados de desempenho
/// individual do usuário/vendedor logado para os últimos 7 dias.
/// </summary>
public class DesempenhoService
{
    private sealed class RawCabItem
    {
        public int Controle { get; set; }
        public int Pedido { get; set; }
        public DateTime Data { get; set; }
        public string? HoraIni { get; set; }
        public string? Cliente { get; set; }
        public string? Status { get; set; }
        public object? Venda { get; set; }
        public object? Cancelada { get; set; }
        public decimal Total { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Desconto { get; set; }
        public int QtdItens { get; set; }

        public bool IsVenda => Venda != null && Convert.ToBoolean(Venda);
        public bool IsCancelada => Cancelada != null && Convert.ToBoolean(Cancelada);
    }

    private sealed class RawProdutoItem
    {
        public string CodInterno { get; set; } = string.Empty;
        public string Produto { get; set; } = string.Empty;
        public decimal Quantidade { get; set; }
        public decimal Total { get; set; }
    }

    private sealed class RawRankingItem
    {
        public int LkVendedor { get; set; }
        public string NomeVendedor { get; set; } = string.Empty;
        public int QuantidadeVendas { get; set; }
    }

    public async Task<DesempenhoVendedorModel> ObterDesempenho7DiasAsync(
        int? lkUsuario,
        int? lkVendedor,
        string? nomeUsuario = null,
        string? nomeVendedor = null)
    {
        var dataInicio = DateTime.Today.AddDays(-6).Date;
        var dataFim = DateTime.Today.Date.AddDays(1).AddTicks(-1);

        var model = new DesempenhoVendedorModel
        {
            DataInicio = dataInicio,
            DataFim = DateTime.Today.Date,
            NomeUsuario = nomeUsuario ?? "Operador",
            NomeVendedor = !string.IsNullOrWhiteSpace(nomeVendedor) 
                ? nomeVendedor 
                : (!string.IsNullOrWhiteSpace(nomeUsuario) ? nomeUsuario : "Vendedor")
        };

        using var conn = DbConnectionFactory.CreateConnection();

        // 1. Monta cláusula WHERE de amarração com o usuário/vendedor logado
        var condicoesFiltro = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("dataInicio", dataInicio);
        parametros.Add("dataFim", dataFim);

        if (lkUsuario.HasValue && lkUsuario.Value > 0 && lkVendedor.HasValue && lkVendedor.Value > 0)
        {
            condicoesFiltro.Add("(ta.LkUsuario = @lkUsuario OR ta.LkVendedor = @lkVendedor)");
            parametros.Add("lkUsuario", lkUsuario.Value);
            parametros.Add("lkVendedor", lkVendedor.Value);
        }
        else if (lkUsuario.HasValue && lkUsuario.Value > 0)
        {
            condicoesFiltro.Add("ta.LkUsuario = @lkUsuario");
            parametros.Add("lkUsuario", lkUsuario.Value);
        }
        else if (lkVendedor.HasValue && lkVendedor.Value > 0)
        {
            condicoesFiltro.Add("ta.LkVendedor = @lkVendedor");
            parametros.Add("lkVendedor", lkVendedor.Value);
        }
        else
        {
            // Se nenhum ID for passado, restringe ao usuário corrente ou nada
            condicoesFiltro.Add("1=1");
        }

        string sqlFiltroUsuario = string.Join(" AND ", condicoesFiltro);

        // 2. Consulta de Cabeçalhos (TabEst3A)
        string sqlCab = $@"
            SELECT 
                ta.Controle,
                ta.Pedido,
                ta.Data,
                ta.HoraIni,
                ta.Cliente,
                ta.Status,
                ta.Venda,
                ta.Cancelada,
                ISNULL(ta.TOTAL, 0) as Total,
                ISNULL(ta.SUBTOTAL, 0) as Subtotal,
                ISNULL(ta.DESCONTO, 0) as Desconto,
                ISNULL(ta.QtdItens, 0) as QtdItens
            FROM TabEst3A ta
            WHERE ta.Data >= @dataInicio AND ta.Data <= @dataFim
              AND {sqlFiltroUsuario}
            ORDER BY ta.Data DESC, ta.Controle DESC";

        IEnumerable<RawCabItem> rawPedidos;
        try
        {
            rawPedidos = await conn.QueryAsync<RawCabItem>(sqlCab, parametros);
        }
        catch
        {
            rawPedidos = Enumerable.Empty<RawCabItem>();
        }

        var listaPedidos = rawPedidos.ToList();

        // 3. Processamento de Totais e Categorias
        foreach (var p in listaPedidos)
        {
            if (p.IsCancelada || p.Status == "C")
            {
                model.QtdCanceladas++;
                model.TotalCanceladas += p.Total;
                continue;
            }

            if (p.Status == "V" || p.IsVenda)
            {
                model.QtdVendas++;
                model.TotalVendas += p.Total;
            }
            else if (p.Status == "O")
            {
                model.QtdOrcamentos++;
                model.TotalOrcamentos += p.Total;
            }
            else
            {
                // Status "P" ou qualquer pré-venda de balcão
                model.QtdPreVendas++;
                model.TotalPreVendas += p.Total;
            }
        }

        // 4. Histórico Diário dos Últimos 7 Dias (Garantindo todos os 7 dias preenchidos)
        var culturaPtBr = new CultureInfo("pt-BR");
        var pedidosValidos = listaPedidos.Where(p => !p.IsCancelada && p.Status != "C").ToList();

        for (int i = 0; i < 7; i++)
        {
            var diaData = dataInicio.AddDays(i);
            var doDia = pedidosValidos.Where(p => p.Data.Date == diaData.Date).ToList();

            string diaNome = diaData.Date == DateTime.Today.Date 
                ? "Hoje" 
                : culturaPtBr.DateTimeFormat.GetAbbreviatedDayName(diaData.DayOfWeek).TrimEnd('.').ToUpper();

            var diaModel = new DesempenhoDiarioModel
            {
                Data = diaData,
                DiaSemana = diaNome,
                DataFormatada = diaData.ToString("dd/MM"),
                QtdOrcamentos = doDia.Count(p => p.Status == "O" && !p.IsVenda),
                QtdPreVendas = doDia.Count(p => p.Status != "O" && p.Status != "V" && !p.IsVenda),
                QtdVendas = doDia.Count(p => p.Status == "V" || p.IsVenda),
                TotalVendido = doDia.Where(p => p.Status == "V" || p.IsVenda).Sum(p => p.Total)
            };

            model.Dias.Add(diaModel);
        }

        // Calcula proporções visuais para o gráfico diário (Apenas vendas reais)
        decimal maxTotalDia = model.Dias.Max(d => d.TotalVendido);
        foreach (var dia in model.Dias)
        {
            if (maxTotalDia > 0 && dia.TotalVendido > 0)
            {
                dia.ProporcaoBarra = (double)(dia.TotalVendido / maxTotalDia);
                dia.AlturaBarraPx = Math.Max(12.0, dia.ProporcaoBarra * 72.0);
            }
            else
            {
                dia.ProporcaoBarra = 0.0;
                dia.AlturaBarraPx = 0.0;
            }
        }

        // 5. Ranking TOP 3 Vendedores da Semana (Baseado estritamente em QUANTIDADE DE VENDAS REAIS - Sem valores)
        string sqlRanking = @"
            SELECT 
                MAX(ISNULL(ta.LkVendedor, 0)) AS LkVendedor,
                COALESCE(
                    NULLIF(RTRIM(LTRIM(v.NOME)), ''), 
                    NULLIF(RTRIM(LTRIM(ta.Vendedor)), ''), 
                    NULLIF(RTRIM(LTRIM(u.USUARIO)), ''), 
                    'Vendedor'
                ) AS NomeVendedor,
                COUNT(*) AS QuantidadeVendas
            FROM TabEst3A ta
            LEFT JOIN TabVend v ON ta.LkVendedor = v.CONTROLE
            LEFT JOIN serv u ON ta.LkUsuario = u.CONTROLE
            WHERE ta.Data >= @dataInicio AND ta.Data <= @dataFim
              AND ISNULL(ta.Cancelada, 0) = 0
              AND (ta.Status = 'V' OR ISNULL(ta.Venda, 0) = 1)
            GROUP BY 
                COALESCE(
                    NULLIF(RTRIM(LTRIM(v.NOME)), ''), 
                    NULLIF(RTRIM(LTRIM(ta.Vendedor)), ''), 
                    NULLIF(RTRIM(LTRIM(u.USUARIO)), ''), 
                    'Vendedor'
                )
            ORDER BY COUNT(*) DESC";

        try
        {
            var rankingGeral = await conn.QueryAsync<RawRankingItem>(sqlRanking, parametros);
            var listaOrdenada = rankingGeral.OrderByDescending(r => r.QuantidadeVendas).ToList();

            int pos = 1;
            int userPos = 0;
            int userQtd = 0;
            var top3 = new List<RankingVendedorModel>();

            foreach (var r in listaOrdenada)
            {
                bool isUser = (lkVendedor.HasValue && lkVendedor.Value > 0 && r.LkVendedor == lkVendedor.Value)
                    || (!string.IsNullOrWhiteSpace(nomeVendedor) && r.NomeVendedor.Equals(nomeVendedor.Trim(), StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(nomeUsuario) && r.NomeVendedor.Equals(nomeUsuario.Trim(), StringComparison.OrdinalIgnoreCase));

                if (isUser && userPos == 0)
                {
                    userPos = pos;
                    userQtd = r.QuantidadeVendas;
                }

                if (pos <= 3)
                {
                    top3.Add(new RankingVendedorModel
                    {
                        Posicao = pos,
                        LkVendedor = r.LkVendedor,
                        NomeVendedor = r.NomeVendedor,
                        QuantidadeVendas = r.QuantidadeVendas,
                        IsUsuarioLogado = isUser
                    });
                }

                pos++;
            }

            model.Top3Vendedores = top3;
            model.PosicaoUsuarioLogado = userPos;
            model.QuantidadeVendasUsuarioLogado = userQtd;

            if (userPos >= 1 && userPos <= 3)
            {
                model.MensagemPosicaoUsuario = $"Parabéns! Você está no Top {userPos} da semana com {userQtd} vendas!";
            }
            else if (userPos > 3)
            {
                model.MensagemPosicaoUsuario = $"Sua posição atual: {userPos}º lugar com {userQtd} vendas • Continue acelerando!";
            }
            else
            {
                model.MensagemPosicaoUsuario = "Faça suas vendas para subir ao pódio da semana!";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DesempenhoService] Falha no cálculo do ranking: {ex}");
            model.Top3Vendedores = new List<RankingVendedorModel>();
            model.MensagemPosicaoUsuario = "Faça suas vendas para subir ao pódio da semana!";
        }

        // 6. Consulta Top 6 Produtos Mais Vendidos pelo Vendedor (TabEst3B)
        string sqlTopProdutos = $@"
            SELECT TOP 6
                ISNULL(tb.CodInterno, '') AS CodInterno,
                ISNULL(tb.Produto, 'Item sem descrição') AS Produto,
                SUM(ISNULL(tb.Quantidade, 0)) AS Quantidade,
                SUM(ISNULL(tb.Total, 0)) AS Total
            FROM TabEst3B tb
            INNER JOIN TabEst3A ta ON tb.LkEst3A = ta.Controle
            WHERE ta.Data >= @dataInicio AND ta.Data <= @dataFim
              AND ISNULL(ta.Cancelada, 0) = 0
              AND {sqlFiltroUsuario}
            GROUP BY tb.CodInterno, tb.Produto
            ORDER BY Total DESC, Quantidade DESC";

        try
        {
            var rawTop = await conn.QueryAsync<RawProdutoItem>(sqlTopProdutos, parametros);
            int pos = 1;
            model.TopProdutos = rawTop.Select(tp => new DesempenhoProdutoModel
            {
                Posicao = pos++,
                CodInterno = tp.CodInterno,
                Produto = tp.Produto,
                Quantidade = tp.Quantidade,
                Total = tp.Total
            }).ToList();
        }
        catch
        {
            model.TopProdutos = new List<DesempenhoProdutoModel>();
        }

        return model;
    }
}
