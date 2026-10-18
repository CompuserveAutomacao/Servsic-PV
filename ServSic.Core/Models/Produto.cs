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
    public string? CodOrigem { get; set; }
    public string? CodFornecedor { get; set; }
    public string? Ncm { get; set; }
    public string? Cest { get; set; }
    public string? Apresentacao { get; set; }
    public string? Fabricante { get; set; }
    public string? Unidade { get; set; } = "UN";
    public decimal PrVenda { get; set; }
    public decimal PrPrazo { get; set; }
    public decimal PrAtacado { get; set; }
    public decimal PrMinimo { get; set; }
    public decimal PrAtacado1 { get; set; }
    public decimal QtdAtacado { get; set; }
    public string? UnAtacado { get; set; }
    public decimal PrCusto { get; set; }
    public decimal Estoque { get; set; }
    public decimal EstoqueLoja { get; set; }
    public decimal EstoqueDeposito { get; set; }
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

    // Identificação:
    // O usuário instruiu que onde está 'CÓD' deve ser o CONTROLE do produto (ID),
    // e o CodInterno é o EAN / Código de Barras principal do banco de dados.
    public string CodigoFormatado => Controle > 0 ? Controle.ToString() : (!string.IsNullOrWhiteSpace(Codigo) ? Codigo : "");
    public string ControleTexto => Controle.ToString();

    // EAN / Código de barras principal (CodInterno no banco)
    public string Ean => !string.IsNullOrWhiteSpace(CodInterno) ? CodInterno.Trim() : (!string.IsNullOrWhiteSpace(CodBarra) ? CodBarra.Trim() : "");
    public string EanFormatado => !string.IsNullOrWhiteSpace(CodInterno) ? CodInterno.Trim() : (!string.IsNullOrWhiteSpace(CodBarra) ? CodBarra.Trim() : "Não informado");
    public bool HasEan => !string.IsNullOrWhiteSpace(CodInterno);

    // Cód. Barras Adicional (CodBarra)
    public string CodBarraAdicional => !string.IsNullOrWhiteSpace(CodBarra) ? CodBarra.Trim() : "";
    public string CodBarraAdicionalFormatado => !string.IsNullOrWhiteSpace(CodBarra) ? CodBarra.Trim() : "Não informado";
    public bool HasCodBarraAdicional => !string.IsNullOrWhiteSpace(CodBarra) && CodBarra.Trim() != CodInterno?.Trim();

    // Cód. Atacado / Origem (CodORIGEM)
    public string CodOrigemFormatado => !string.IsNullOrWhiteSpace(CodOrigem) ? CodOrigem.Trim() : "Não informado";
    public bool HasCodOrigem => !string.IsNullOrWhiteSpace(CodOrigem);

    // Cód. Caixa / Atacado (CodBarraCx)
    public string CodBarraCxFormatado => !string.IsNullOrWhiteSpace(CodBarraCx) ? CodBarraCx.Trim() : "Não informado";
    public bool HasCodBarraCx => !string.IsNullOrWhiteSpace(CodBarraCx) && CodBarraCx.Trim() != CodBarra?.Trim() && CodBarraCx.Trim() != CodInterno?.Trim();

    // Cód. Fornecedor (CodFornecedor)
    public string CodFornecedorFormatado => !string.IsNullOrWhiteSpace(CodFornecedor) ? CodFornecedor.Trim() : "Não informado";
    public bool HasCodFornecedor => !string.IsNullOrWhiteSpace(CodFornecedor);

    // NCM e CEST (Fiscal)
    public string NcmFormatado => !string.IsNullOrWhiteSpace(Ncm) ? Ncm.Trim() : "Não informado";
    public bool HasNcm => !string.IsNullOrWhiteSpace(Ncm);
    public string CestFormatado => !string.IsNullOrWhiteSpace(Cest) ? Cest.Trim() : "Não informado";
    public bool HasCest => !string.IsNullOrWhiteSpace(Cest);

    // Apresentação
    public string ApresentacaoFormatada => !string.IsNullOrWhiteSpace(Apresentacao) ? Apresentacao.Trim() : "Não informada";
    public bool HasApresentacao => !string.IsNullOrWhiteSpace(Apresentacao);

    // Localização (Moeda)
    public string LocalizacaoFormatada => !string.IsNullOrWhiteSpace(Localizacao) ? Localizacao.Trim() : "Não informada";
    public bool HasLocalizacao => !string.IsNullOrWhiteSpace(Localizacao);

    // Preços e Estoque Formatados
    public string PrVendaFormatado => PrVenda.ToString("C2");
    public string PrVendaValorFormatado => PrVenda.ToString("N2");
    public string PrPrazoFormatado => PrPrazo > 0 ? PrPrazo.ToString("C2") : "Não informado";
    public string PrAtacadoFormatado => PrAtacado > 0 ? PrAtacado.ToString("C2") : (PrAtacado1 > 0 ? PrAtacado1.ToString("C2") : "Não informado");
    public string PrMinimoFormatado => PrMinimo > 0 ? PrMinimo.ToString("C2") : "Não informado";
    public string RegraAtacadoDescricao => QtdAtacado > 0 
        ? $"A partir de {QtdAtacado:N0} {(string.IsNullOrWhiteSpace(UnAtacado) ? Unidade : UnAtacado)}"
        : "Sem quantidade mínima fixada";
    public string PrCustoFormatado => PrCusto.ToString("C2");

    public string EstoqueFormatado => $"{Estoque:N2} {Unidade}";
    public string EstoqueLojaFormatado => $"{EstoqueLoja:N2} {Unidade}";
    public string EstoqueDepositoFormatado => $"{EstoqueDeposito:N2} {Unidade}";
    public string EstMinimoFormatado => $"{EstMinimo:N2} {Unidade}";
    public string EstoqueNumeroFormatado => Estoque.ToString("N2");
    public bool HasEstoque => Estoque > 0;
    public bool HasEstoqueDeposito => EstoqueDeposito > 0;
    public string StatusEstoqueDescricao => HasEstoque ? $"{Estoque:N2} {Unidade}" : "Sem Estoque (0)";

    // Helpers visuais modernos para badges de estoque
    public string EstoqueBadgeBg => HasEstoque ? "#ECFDF5" : "#FEF2F2";
    public string EstoqueBadgeBorder => HasEstoque ? "#A7F3D0" : "#FECACA";
    public string EstoqueBadgeForeground => HasEstoque ? "#065F46" : "#991B1B";
    public string EstoqueBadgeDotColor => HasEstoque ? "#059669" : "#DC2626";

    public string FabricanteFormatado => !string.IsNullOrWhiteSpace(Fabricante) ? Fabricante.Trim() : "Sem marca/fabricante";
    public bool HasFabricante => !string.IsNullOrWhiteSpace(Fabricante);
    public string GrupoFormatado => !string.IsNullOrWhiteSpace(Grupo) ? Grupo.Trim() : "Sem grupo";
    public bool HasGrupo => !string.IsNullOrWhiteSpace(Grupo);
    public bool HasAtacado => PrAtacado > 0 || PrAtacado1 > 0;
    public string PerDescMaxFormatado => PerDescMax > 0 ? $"{PerDescMax:N2}%" : "Sem limite fixado";

    public bool HasFoto => (FotoBytes != null && FotoBytes.Length > 0) || !string.IsNullOrWhiteSpace(FotoPath);
    public string ExibicaoResumida => $"{CodigoFormatado} - {Descricao} | {PrVenda:C2}";
}
