using System;
using System.Collections.Generic;
using System.Linq;

namespace ServSic.Core.Models;

public class PedidoVenda
{
    public int Controle { get; set; }
    public int Pedido { get; set; }
    public DateTime Data { get; set; } = DateTime.Today;
    public string HoraIni { get; set; } = DateTime.Now.ToString("HH:mm");
    public int? LkCliente { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public int? LkVendedor { get; set; }
    public string? Vendedor { get; set; }

    public decimal Subtotal { get; set; } = 0m;
    public decimal Desconto { get; set; } = 0m;
    public decimal Acrescimo { get; set; } = 0m;
    public decimal Total { get; set; } = 0m;
    public decimal VlPago { get; set; } = 0m;
    public int QtdItens { get; set; } = 0;

    /// <summary>
    /// Status: 'O' = Orçamento, 'V' = Venda / Pedido
    /// </summary>
    public string Status { get; set; } = "O";

    /// <summary>
    /// Venda: 0 = Não efetivado como venda, 1 = Venda efetivada
    /// </summary>
    public int Venda { get; set; } = 0;
    public int Cancelada { get; set; } = 0;
    public int? LkUsuario { get; set; }
    public int IdEmpresa { get; set; } = 1;

    public string? Endereco { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? UF { get; set; }
    public string? CEP { get; set; }
    public string? CPF_CNPJ { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? ComplEnd { get; set; }
    public string? Obs { get; set; }

    public List<PedidoItem> Itens { get; set; } = new();
    public List<FormaPagamento> Pagamentos { get; set; } = new();

    public bool IsOrcamento => Status == "O";
    public bool IsVenda => Status == "V" || Venda == 1;

    public string StatusDescricao => IsOrcamento ? "Orçamento" : (Cancelada == 1 ? "Cancelado" : "Pedido de Venda");

    public string NumeroFormatado => Pedido > 0 ? $"Nº {Pedido}" : "Novo Pedido";
    public string DataFormatada => Data.ToString("dd/MM/yyyy");
    public string DocFormatado => !string.IsNullOrWhiteSpace(CPF_CNPJ) ? $"Doc: {CPF_CNPJ}" : "";
    public string TelFormatado => !string.IsNullOrWhiteSpace(Telefone) ? $"Tel: {Telefone}" : "";
    public string CidadeFormatada => !string.IsNullOrWhiteSpace(Cidade) ? $"Cidade: {Cidade}" : "";

    public void RecalcularTotais()
    {
        foreach (var item in Itens)
        {
            item.RecalcularTotal();
        }

        Subtotal = Itens.Sum(i => i.SubtotalBruto);
        decimal somaDescontosItens = Itens.Sum(i => i.Desconto);
        decimal somaAcrescimosItens = Itens.Sum(i => i.Acrescimo);

        // O total geral respeita os descontos dos itens e adicionais globais
        decimal totalFinal = Subtotal - (somaDescontosItens + Desconto) + (somaAcrescimosItens + Acrescimo);
        Total = totalFinal < 0 ? 0 : totalFinal;
        QtdItens = Itens.Count;
    }
}
