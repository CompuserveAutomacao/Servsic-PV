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
    public string? ComplEnd { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? CEP { get; set; }
    public string? Cep { get => CEP; set => CEP = value; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? Email { get; set; }
    public string? RefBan { get; set; }
    public string? Obs { get; set; }

    public decimal LimiteCred { get; set; } = 0m;
    public string? Inativo { get; set; } = "N";
    public decimal SaldoDevedor { get; set; } = 0m;
    public decimal TotalVencido { get; set; } = 0m;
    public int TitulosVencidos { get; set; } = 0;

    public bool HasEndereco => !string.IsNullOrWhiteSpace(Endereco) || !string.IsNullOrWhiteSpace(Bairro) || !string.IsNullOrWhiteSpace(Cidade);

    public string EnderecoFormatado
    {
        get
        {
            var partes = new System.Collections.Generic.List<string>();

            // Logradouro, Número e Complemento
            if (!string.IsNullOrWhiteSpace(Endereco))
            {
                string logradouro = Endereco.Trim();
                if (!string.IsNullOrWhiteSpace(Numero))
                    logradouro += $", {Numero.Trim()}";
                if (!string.IsNullOrWhiteSpace(ComplEnd) &&
                    !string.Equals(ComplEnd.Trim(), Endereco.Trim(), System.StringComparison.OrdinalIgnoreCase) &&
                    !Endereco.Trim().Contains(ComplEnd.Trim(), System.StringComparison.OrdinalIgnoreCase))
                {
                    logradouro += $" ({ComplEnd.Trim()})";
                }
                partes.Add(logradouro);
            }
            else if (!string.IsNullOrWhiteSpace(Numero))
            {
                string num = $"Nº {Numero.Trim()}";
                if (!string.IsNullOrWhiteSpace(ComplEnd))
                    num += $" ({ComplEnd.Trim()})";
                partes.Add(num);
            }
            else if (!string.IsNullOrWhiteSpace(ComplEnd))
            {
                partes.Add(ComplEnd.Trim());
            }

            // Bairro
            if (!string.IsNullOrWhiteSpace(Bairro))
                partes.Add($"Bairro: {Bairro.Trim()}");

            // Cidade / UF
            if (!string.IsNullOrWhiteSpace(Cidade))
            {
                string cidUf = Cidade.Trim();
                if (!string.IsNullOrWhiteSpace(Estado))
                    cidUf += $" - {Estado.Trim()}";
                partes.Add(cidUf);
            }
            else if (!string.IsNullOrWhiteSpace(Estado))
            {
                partes.Add(Estado.Trim());
            }

            // CEP
            if (!string.IsNullOrWhiteSpace(CEP))
                partes.Add($"CEP: {CEP.Trim()}");

            return partes.Count > 0 ? string.Join(" • ", partes) : "Endereço não cadastrado";
        }
    }

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
