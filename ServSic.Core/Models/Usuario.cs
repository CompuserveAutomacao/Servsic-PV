namespace ServSic.Core.Models;

public class Usuario
{
    public int Controle { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? CodSenha { get; set; }
    public int Nivel { get; set; }
    public int? LkVendedor { get; set; }
    public string? NomeVendedor { get; set; }

    public string NivelDescricao => Nivel switch
    {
        0 => "Operador",
        1 => "Supervisor",
        2 => "Gerente",
        _ => $"Nível {Nivel}"
    };

    public decimal LimiteDesconto { get; set; } = 0m;
    public string LimiteDescontoFormatado => LimiteDesconto > 0 ? $"{LimiteDesconto:N2}%" : "0,00%";
}
