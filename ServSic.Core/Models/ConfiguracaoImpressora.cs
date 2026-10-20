namespace ServSic.Core.Models;

/// <summary>
/// Configurações da impressora térmica para cupom não-fiscal (balcão / pré-venda / orçamento).
/// </summary>
public class ConfiguracaoImpressora
{
    /// <summary>
    /// Nome da impressora no Windows (ex: "POS-80", "EPSON TM-T20", "MP-4200 TH")
    /// </summary>
    public string NomeImpressora { get; set; } = string.Empty;

    /// <summary>
    /// Largura da bobina em colunas de texto (42 colunas para 80mm padrão Epson TM-T20 / Bematech; 32 colunas para 58mm)
    /// </summary>
    public int LarguraColunas { get; set; } = 42;

    /// <summary>
    /// Ação padrão após finalizar:
    /// "Previa" = Exibir cupom na tela para conferência
    /// "Direto" = Imprimir direto na impressora
    /// "NaoImprimir" = Não imprimir automaticamente
    /// </summary>
    public string ModoImpressao { get; set; } = "Previa";

    /// <summary>
    /// Se true, aciona guilhotina da impressora ao final da impressão
    /// </summary>
    public bool CortarPapel { get; set; } = true;

    /// <summary>
    /// Se true, imprime duas vias automaticamente (via loja / via cliente)
    /// </summary>
    public bool DuasVias { get; set; } = false;

    /// <summary>
    /// Mensagem opcional de rodapé no cupom
    /// </summary>
    public string MensagemRodape { get; set; } = "OBRIGADO PELA PREFERENCIA!";

    public bool IsBobina80mm => LarguraColunas >= 40;
}
