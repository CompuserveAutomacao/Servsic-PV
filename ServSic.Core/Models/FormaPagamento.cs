using System;

namespace ServSic.Core.Models;

public class FormaPagamento
{
    public int Controle { get; set; }
    public int Pedido { get; set; }
    public string Recebimento { get; set; } = "DINHEIRO";
    public int LkTipo { get; set; } = 1;
    public decimal Valor { get; set; }
    public int Parcela { get; set; } = 1;
    public string GeraConta { get; set; } = "N";
    public DateTime? Vencimento { get; set; } = DateTime.Today;
    public string? Caixa { get; set; }
    public string? Operador { get; set; }
}
