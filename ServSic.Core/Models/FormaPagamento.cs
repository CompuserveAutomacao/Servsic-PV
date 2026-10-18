using System;

namespace ServSic.Core.Models;

/// <summary>
/// Representa uma parcela ou forma de pagamento vinculada ao pedido na tabela TabEst3C.
/// </summary>
public class FormaPagamento
{
    public int Controle { get; set; }
    public int Pedido { get; set; }
    public string Recebimento { get; set; } = "DINHEIRO";
    public int LkTipo { get; set; } = 1;
    public DateTime Data { get; set; } = DateTime.Today;
    public decimal Total { get; set; }
    public decimal Valor { get; set; }
    public int Parcela { get; set; } = 1;
    public string Caixa { get; set; } = "LOJA";
    public int LkCaixa { get; set; } = 1;
    public string Operador { get; set; } = "COMPUSERVE";
    public int LkOperador { get; set; } = 1;
    public string Hora { get; set; } = DateTime.Now.ToString("HH:mm");
    public bool GeraConta { get; set; }
    public DateTime Vencimento { get; set; } = DateTime.Today;
    public int AtivaTroco { get; set; }
    public int LkTroco { get; set; }
    public string? TipoTroco { get; set; }
    public decimal VlTroco { get; set; }
    public int IdEmpresa { get; set; } = 1;
    public int? LkConta { get; set; }
    public string? Conta { get; set; }
    public string? NumDoc { get; set; }
    public decimal Divida { get; set; }
    public decimal VlFatura { get; set; }
    public string? CAut { get; set; }
    public string? Autorizacao { get => CAut; set => CAut = value; }

    // Helpers de exibição
    public string ValorFormatado => Valor.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    public string VencimentoFormatado => Vencimento.ToString("dd/MM/yyyy");
    public string VencimentoDescricao => $"Venc: {VencimentoFormatado}";
    public string ParcelaFormatada => $"{Parcela}ª Parcela";
    public string ParcelaDescricao => $"Parc. {Parcela}";
    public string TrocoFormatado => VlTroco.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    public bool TrocoVisivel => VlTroco > 0;
}
