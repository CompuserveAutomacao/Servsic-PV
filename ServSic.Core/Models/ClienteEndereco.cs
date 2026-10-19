using System;

namespace ServSic.Core.Models;

public class ClienteEndereco
{
    public int Controle { get; set; }
    public int LkCliente { get; set; }
    public string Apelido { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public string? Numero { get; set; }
    public string? ComplEnd { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? Cep { get; set; }
    public int? CodMunicipio { get; set; }
    public string? Obs { get; set; }
    public bool Inativo { get; set; }
    public DateTime? DtUltAtualizacao { get; set; }

    /// <summary>
    /// Endereço formatado legível para exibição em listas e cards
    /// </summary>
    public string EnderecoFormatado
    {
        get
        {
            var partes = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(Endereco))
            {
                string endNum = Endereco;
                if (!string.IsNullOrWhiteSpace(Numero)) endNum += $", {Numero}";
                partes.Add(endNum);
            }
            if (!string.IsNullOrWhiteSpace(ComplEnd)) partes.Add(ComplEnd);
            if (!string.IsNullOrWhiteSpace(Bairro)) partes.Add(Bairro);
            if (!string.IsNullOrWhiteSpace(Cidade))
            {
                string cidUf = Cidade;
                if (!string.IsNullOrWhiteSpace(Estado)) cidUf += $" - {Estado}";
                partes.Add(cidUf);
            }
            if (!string.IsNullOrWhiteSpace(Cep)) partes.Add($"CEP: {Cep}");
            return string.Join(" - ", partes);
        }
    }

    public string ApelidoComEndereco => !string.IsNullOrWhiteSpace(Apelido)
        ? $"{Apelido} — {EnderecoFormatado}"
        : EnderecoFormatado;
}
