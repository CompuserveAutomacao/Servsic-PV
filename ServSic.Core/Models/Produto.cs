namespace ServSic.Core.Models;

public class Produto
{
    public int Controle { get; set; }
    public string? Codigo { get; set; }
    public string? CodInterno { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? DescricaoDetalhada { get; set; }
    public string? CodBarra { get; set; }
    public string? CodBarraCx { get; set; }
    public string? Fabricante { get; set; }
    public string? Unidade { get; set; } = "UN";
    public decimal PrVenda { get; set; }
    public decimal PrCusto { get; set; }
    public decimal Estoque { get; set; }
    public decimal EstMinimo { get; set; }
    public string? Localizacao { get; set; } // Campo Moeda na TabEst1
    public int? LkSetor { get; set; }
    public string? Grupo { get; set; } // Setor na TabEst8
    public int? LkSubGrupo { get; set; }
    public string? SubGrupo { get; set; } // Descricao na TabEst8b
    public decimal PerDescMax { get; set; }
    public bool Inativo { get; set; }
    public byte[]? FotoBytes { get; set; }
    public string? FotoPath { get; set; }

    // Propriedades auxiliares de exibição e formatação
    public string CodigoFormatado => !string.IsNullOrWhiteSpace(CodInterno) 
        ? CodInterno 
        : (!string.IsNullOrWhiteSpace(Codigo) ? Codigo : Controle.ToString());

    public string PrVendaFormatado => PrVenda.ToString("C2");
    public string PrCustoFormatado => PrCusto.ToString("C2");
    public string EstoqueFormatado => $"{Estoque:N2} {Unidade}";
    public string EstoqueNumeroFormatado => Estoque.ToString("N2");
    public string LocalizacaoFormatada => !string.IsNullOrWhiteSpace(Localizacao) ? Localizacao.Trim() : "Não informada";
    public string FabricanteFormatado => !string.IsNullOrWhiteSpace(Fabricante) ? Fabricante.Trim() : "Sem marca/fabricante";
    public string GrupoFormatado => !string.IsNullOrWhiteSpace(Grupo) ? Grupo.Trim() : "Sem grupo";
    public string PerDescMaxFormatado => PerDescMax > 0 ? $"{PerDescMax:N2}%" : "Sem limite fixado";

    public bool HasEstoque => Estoque > 0;
    public bool HasFoto => (FotoBytes != null && FotoBytes.Length > 0) || !string.IsNullOrWhiteSpace(FotoPath);
    public string ExibicaoResumida => $"{CodigoFormatado} - {Descricao} | {PrVenda:C2}";
}
