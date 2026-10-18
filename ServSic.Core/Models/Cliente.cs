namespace ServSic.Core.Models;

public class Cliente
{
    public int Controle { get; set; }
    public string? Codigo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Fantasia { get; set; }
    public string? Cpf { get; set; }
    public string? CGC { get; set; }
    public string? Endereco { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? CEP { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }

    public string Documento => !string.IsNullOrWhiteSpace(CGC) ? CGC : (Cpf ?? string.Empty);
    public string IdentificacaoCompleta => $"{Codigo} - {Nome} {(string.IsNullOrWhiteSpace(Documento) ? "" : $"({Documento})")}".Trim();
}
