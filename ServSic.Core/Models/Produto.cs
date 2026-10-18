namespace ServSic.Core.Models;

public class Produto
{
    public int Controle { get; set; }
    public string? CodInterno { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? CodBarra { get; set; }
    public string? Fabricante { get; set; }
    public string? Unidade { get; set; }
    public decimal PrVenda { get; set; }
    public decimal PrCusto { get; set; }
    public decimal Estoque { get; set; }
    public int? LkSetor { get; set; }
    public decimal PerDescMax { get; set; }

    public string ExibicaoResumida => $"{CodInterno} - {Descricao} | {PrVenda:C2}";
}
