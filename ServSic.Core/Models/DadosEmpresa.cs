namespace ServSic.Core.Models;

/// <summary>
/// Dados da empresa para impressão do cabeçalho do cupom não-fiscal
/// </summary>
public class DadosEmpresa
{
    public string NomeFantasia { get; set; } = "COMPUSERVE SISTEMAS";
    public string RazaoSocial { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string InscricaoEstadual { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string UF { get; set; } = string.Empty;
    public string CEP { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;

    public string EnderecoLinha1
    {
        get
        {
            var end = Endereco;
            if (!string.IsNullOrWhiteSpace(Numero)) end += $", {Numero}";
            if (!string.IsNullOrWhiteSpace(Bairro)) end += $" - {Bairro}";
            return end;
        }
    }

    public string EnderecoLinha2
    {
        get
        {
            var cid = Cidade;
            if (!string.IsNullOrWhiteSpace(UF)) cid += $"/{UF}";
            if (!string.IsNullOrWhiteSpace(CEP)) cid += $" - CEP: {CEP}";
            return cid;
        }
    }
}
