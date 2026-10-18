namespace ServSic.Core.Models;

public class PedidoItem
{
    public int Controle { get; set; }
    public int LkEst3A { get; set; }
    public int Pedido { get; set; }
    public int LkProduto { get; set; }
    public string? CodInterno { get; set; }
    public string? CodBarra { get; set; }
    public string Produto { get; set; } = string.Empty;
    public decimal Quantidade { get; set; } = 1m;
    public decimal VlUnitario { get; set; } = 0m;
    public decimal Desconto { get; set; } = 0m;
    public decimal Acrescimo { get; set; } = 0m;
    public decimal Total { get; set; } = 0m;
    public decimal PrecoVenda { get; set; } = 0m;
    public decimal PrCusto { get; set; } = 0m;
    public string? Unidade { get; set; } = "UN";
    public string? Marca { get; set; }
    public int? LkVendedor { get; set; }
    public string? Vendedor { get; set; }

    public decimal SubtotalBruto => Quantidade * VlUnitario;

    public void RecalcularTotal()
    {
        Total = SubtotalBruto - Desconto + Acrescimo;
        if (Total < 0) Total = 0;
    }
}
