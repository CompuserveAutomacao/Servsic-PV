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

    public decimal LimiteCred { get; set; } = 0m;
    public string? Inativo { get; set; } = "N";
    public decimal SaldoDevedor { get; set; } = 0m;
    public decimal TotalVencido { get; set; } = 0m;
    public int TitulosVencidos { get; set; } = 0;

    public decimal LimiteDisponivel => LimiteCred > 0 ? Math.Max(0m, LimiteCred - SaldoDevedor) : 0m;
    public bool HasDividaAberta => SaldoDevedor > 0;
    public bool HasTitulosVencidos => TotalVencido > 0;

    public bool IsBloqueado => (Inativo != null && Inativo.Trim().ToUpper() == "S") || TotalVencido > 0 || (LimiteCred > 0 && SaldoDevedor >= LimiteCred);

    public string StatusCredito => IsBloqueado ? "BLOQUEADO" : "DESBLOQUEADO";

    public string StatusCreditoDescricao
    {
        get
        {
            if (Inativo != null && Inativo.Trim().ToUpper() == "S")
                return "Cliente Bloqueado (Cadastro Inativo no Sistema)";
            if (TotalVencido > 0)
                return $"Cliente Bloqueado ({TitulosVencidos} título(s) vencido(s) totalizando R$ {TotalVencido:N2})";
            if (LimiteCred > 0 && SaldoDevedor >= LimiteCred)
                return $"Cliente Bloqueado (Limite de Crédito Excedido: Saldo Devedor R$ {SaldoDevedor:N2} >= Limite R$ {LimiteCred:N2})";
            return "Cliente Liberado / Desbloqueado para compras a prazo";
        }
    }

    public string LimiteFormatado => $"R$ {LimiteCred:N2}";
    public string DisponivelFormatado => $"R$ {LimiteDisponivel:N2}";
    public string SaldoDevedorFormatado => $"R$ {SaldoDevedor:N2}";
    public string TotalVencidoFormatado => $"R$ {TotalVencido:N2}";
    public string DocumentoFormatado => !string.IsNullOrWhiteSpace(Documento) ? Documento : "-";
    public string CidadeUF => !string.IsNullOrWhiteSpace(Cidade) 
        ? (!string.IsNullOrWhiteSpace(Estado) ? $"{Cidade}/{Estado}" : Cidade) 
        : (Estado ?? "-");

    public string Documento => !string.IsNullOrWhiteSpace(CGC) ? CGC : (Cpf ?? string.Empty);
    public string IdentificacaoCompleta => $"{Codigo} - {Nome} {(string.IsNullOrWhiteSpace(Documento) ? "" : $"({Documento})")}".Trim();
}
