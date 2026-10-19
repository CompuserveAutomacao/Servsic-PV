namespace ServSic.Core.Models;

public class ConfiguracaoVenda
{
    /// <summary>
    /// CFG_ORCAMENTO.ESTOQUEINSUFICIENTE ('S' / 'N')
    /// Quando falso, bloqueia o lançamento e a totalização de orçamento com estoque insuficiente.
    /// </summary>
    public bool HabilitaOrcamentoComEstoqueInsuficiente { get; set; }

    /// <summary>
    /// CFG_PREVENDA.ESTOQUEINSUFICIENTE ('S' / 'N')
    /// Quando falso, bloqueia o lançamento e a totalização de pré-venda com estoque insuficiente.
    /// </summary>
    public bool HabilitaPreVendaComEstoqueInsuficiente { get; set; }

    /// <summary>
    /// CFG_VENDA.ESTOQUEINSUFICIENTE ('S' / 'N')
    /// </summary>
    public bool HabilitaVendaComEstoqueInsuficiente { get; set; }

    /// <summary>
    /// CFG_VENDA.BloqueiaTotalizacaoEstoqueInsuficiente ('S' / 'N')
    /// </summary>
    public bool BloqueiaTotalizacaoEstoqueInsuficiente { get; set; }

    /// <summary>
    /// CFG_VENDA.PRECOMENOR ('S' / 'N')
    /// Habilita venda com preço menor que tabela
    /// </summary>
    public bool HabilitaPrecoMenorQueTabela { get; set; }

    /// <summary>
    /// CFG_VENDA.ImpParaEntregaAutomaticamente ('S' / 'N')
    /// Quando verdadeiro, imprime automaticamente a via dos itens marcados para entrega ao finalizar venda/pré-venda.
    /// </summary>
    public bool ImprimeItensEntregaAutomaticamente { get; set; }

    /// <summary>
    /// CFG_VENDA.BloqueiaAlteracaoCliente ('S' / 'N')
    /// </summary>
    public bool BloqueiaAlteracaoCliente { get; set; }
}
