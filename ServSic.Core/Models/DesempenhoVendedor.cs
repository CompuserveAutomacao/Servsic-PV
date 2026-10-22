using System;
using System.Collections.Generic;

namespace ServSic.Core.Models;

public class DesempenhoVendedorModel
{
    public string NomeVendedor { get; set; } = string.Empty;
    public string NomeUsuario { get; set; } = string.Empty;
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }

    // Totais e Quantidades nos 7 dias
    public int QtdOrcamentos { get; set; }
    public decimal TotalOrcamentos { get; set; }

    public int QtdPreVendas { get; set; }
    public decimal TotalPreVendas { get; set; }

    public int QtdVendas { get; set; }
    public decimal TotalVendas { get; set; }

    public int QtdCanceladas { get; set; }
    public decimal TotalCanceladas { get; set; }

    public int TotalAtendimentos => QtdVendas + QtdPreVendas + QtdOrcamentos;
    public decimal TotalFaturado => TotalVendas; // Faturado = exclusivamente vendas reais concluídas no caixa
    public decimal TotalGeral => TotalVendas + TotalPreVendas + TotalOrcamentos;

    // Indicadores Chave
    public decimal TicketMedioVendas => QtdVendas > 0 ? Math.Round(TotalVendas / QtdVendas, 2) : 0m;
    public decimal TicketMedioGeral => TotalAtendimentos > 0 
        ? Math.Round(TotalGeral / TotalAtendimentos, 2) 
        : 0m;

    public decimal TaxaConversao => TotalAtendimentos > 0
        ? Math.Round(((decimal)QtdVendas * 100m) / TotalAtendimentos, 1)
        : 0m;

    // Percentuais para Gráfico Radial / Anéis (Baseados na Quantidade de Atendimentos)
    public double PercentualVendas => TotalAtendimentos > 0 
        ? Math.Round(((double)QtdVendas / (double)TotalAtendimentos) * 100.0, 1) 
        : 0.0;

    public double PercentualPreVendas => TotalAtendimentos > 0 
        ? Math.Round(((double)QtdPreVendas / (double)TotalAtendimentos) * 100.0, 1) 
        : 0.0;

    public double PercentualOrcamentos => TotalAtendimentos > 0 
        ? Math.Round(((double)QtdOrcamentos / (double)TotalAtendimentos) * 100.0, 1) 
        : 0.0;

    public string PercentualVendasFormatado => $"{PercentualVendas:N1}%";
    public string PercentualPreVendasFormatado => $"{PercentualPreVendas:N1}%";
    public string PercentualOrcamentosFormatado => $"{PercentualOrcamentos:N1}%";

    // Detalhes por dia (Últimos 7 dias)
    public List<DesempenhoDiarioModel> Dias { get; set; } = new();

    // Produtos mais vendidos nos últimos 7 dias
    public List<DesempenhoProdutoModel> TopProdutos { get; set; } = new();

    // Ranking Top 3 Vendedores da Semana (Baseado estritamente em Quantidade de Vendas - Sem valores)
    public List<RankingVendedorModel> Top3Vendedores { get; set; } = new();
    public int PosicaoUsuarioLogado { get; set; }
    public int QuantidadeVendasUsuarioLogado { get; set; }
    public string MensagemPosicaoUsuario { get; set; } = string.Empty;
    public bool IsUsuarioNoTop3 => PosicaoUsuarioLogado >= 1 && PosicaoUsuarioLogado <= 3;
}

public class RankingVendedorModel
{
    public int Posicao { get; set; } // 1, 2, 3
    public int? LkVendedor { get; set; }
    public string NomeVendedor { get; set; } = string.Empty;
    public int QuantidadeVendas { get; set; }
    public bool IsUsuarioLogado { get; set; }

    public string PosicaoFormatada => $"{Posicao}º";
    public string PódioMedalha => $"{Posicao}º";
    public string QuantidadeVendasFormatada => $"{QuantidadeVendas} {(QuantidadeVendas == 1 ? "venda" : "vendas")}";

    public string BadgeFundoCor => Posicao switch
    {
        1 => "#FEF3C7", // Dourado sutil
        2 => "#F1F5F9", // Prata sutil
        3 => "#FFEDD5", // Bronze sutil
        _ => "#F8FAFC"
    };

    public string BadgeTextoCor => Posicao switch
    {
        1 => "#B45309",
        2 => "#475569",
        3 => "#C2410C",
        _ => "#0F172A"
    };

    public string IconeTrofeuCor => Posicao switch
    {
        1 => "#F59E0B",
        2 => "#94A3B8",
        3 => "#F97316",
        _ => "#CBD5E1"
    };
}

public class DesempenhoDiarioModel
{
    public DateTime Data { get; set; }
    public string DiaSemana { get; set; } = string.Empty;
    public string DataFormatada { get; set; } = string.Empty;
    public int QtdOrcamentos { get; set; }
    public int QtdPreVendas { get; set; }
    public int QtdVendas { get; set; }
    public int TotalDocumentos => QtdVendas;
    public decimal TotalVendido { get; set; } // Apenas vendas reais concluídas
    public string TotalVendidoFormatado => TotalVendido > 0 
        ? (TotalVendido % 1 == 0 ? $"R$ {TotalVendido:N0}" : $"R$ {TotalVendido:N2}") 
        : "R$ 0";
    public string TotalDocumentosFormatado => $"{QtdVendas} {(QtdVendas == 1 ? "venda" : "vendas")}";
    public decimal TicketMedio => QtdVendas > 0 
        ? Math.Round(TotalVendido / QtdVendas, 2) 
        : 0m;
    public double ProporcaoBarra { get; set; } // 0.0 a 1.0 em relação ao dia de pico
    public double AlturaBarraPx { get; set; } // Altura em pixels para gráfico (ex: 8 a 80)
}

public class DesempenhoProdutoModel
{
    public int Posicao { get; set; }
    public string PosicaoFormatada => $"#{Posicao}";
    public string CodInterno { get; set; } = string.Empty;
    public string CodigoFormatado => $"Cód: {CodInterno}";
    public string Produto { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal Total { get; set; }
    public string QuantidadeFormatada => Quantidade % 1 == 0 ? $"{Quantidade:N0}" : $"{Quantidade:N2}";
    public string QuantidadeFormatadaComUnidade => $"{QuantidadeFormatada} un";
    public string TotalFormatado => $"R$ {Total:N2}";
}
