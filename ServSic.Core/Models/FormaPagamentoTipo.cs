namespace ServSic.Core.Models;

/// <summary>
/// Representa uma forma de pagamento cadastrada no sistema (tabela TABEST7)
/// com amarração contábil ao Plano de Contas (tabela TabCx2).
/// </summary>
public class FormaPagamentoTipo
{
    public int Controle { get; set; }
    public string Recebimento { get; set; } = string.Empty;
    public string? ContaRec { get; set; }
    public int? LkConta { get; set; }
    public string? PlanoDescricao { get; set; }
    public bool GeraConta { get; set; }
    public bool AtivaTef { get; set; }
    public bool AtivaTroco { get; set; }
    public string? TipoPagNFe { get; set; }
    public int AtalhoNumero { get; set; }

    /// <summary>
    /// Ícone ou símbolo para identificação visual rápida no PDV
    /// </summary>
    public string IconeSimbolo => Recebimento.ToUpperInvariant() switch
    {
        var s when s.Contains("DINHEIRO") => "Money",
        var s when s.Contains("PIX") => "QrCode",
        var s when s.Contains("DEBITO") => "PaymentCard",
        var s when s.Contains("CREDITO") || s.Contains("CREDI-SHOP") => "PaymentCard",
        var s when s.Contains("CARNE") || s.Contains("DUPLICATA") => "DocumentText",
        var s when s.Contains("CHEQUE") => "Document",
        var s when s.Contains("BOLETO") => "BarcodeScanner",
        _ => "Wallet"
    };

    public string AtalhoTexto => AtalhoNumero > 0 ? $"{AtalhoNumero}" : "";
    public string DescricaoFormatada => Recebimento?.Trim() ?? string.Empty;
    public string ContaDescricao => !string.IsNullOrWhiteSpace(PlanoDescricao) ? PlanoDescricao : (!string.IsNullOrWhiteSpace(ContaRec) ? $"Conta: {ContaRec}" : "Caixa Geral");
    public string TipoPrazoLabel => GeraConta ? "A PRAZO" : "À VISTA";
    public bool IsCartao => Recebimento.Contains("CREDITO", StringComparison.OrdinalIgnoreCase) || 
                            Recebimento.Contains("DEBITO", StringComparison.OrdinalIgnoreCase) || 
                            Recebimento.Contains("CREDI-SHOP", StringComparison.OrdinalIgnoreCase);
    public bool IsPix => Recebimento.Contains("PIX", StringComparison.OrdinalIgnoreCase);
    public bool IsDinheiro => Recebimento.Contains("DINHEIRO", StringComparison.OrdinalIgnoreCase);
}
