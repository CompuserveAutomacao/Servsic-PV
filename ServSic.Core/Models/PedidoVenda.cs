using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ServSic.Core.Models;

public class PedidoVenda
{
    public int Controle { get; set; }
    public int Pedido { get; set; }
    public DateTime Data { get; set; } = DateTime.Today;
    public string HoraIni { get; set; } = DateTime.Now.ToString("HH:mm");
    public int? LkCliente { get; set; }
    public string? Cliente { get; set; }
    public int? LkVendedor { get; set; }
    public string? Vendedor { get; set; }

    public decimal Subtotal { get; set; } = 0m;
    public decimal Desconto { get; set; } = 0m;
    public decimal Acrescimo { get; set; } = 0m;
    public decimal Total { get; set; } = 0m;
    public decimal VlPago { get; set; } = 0m;
    public decimal Faturado { get; set; } = 0m;
    public int QtdItens { get; set; } = 0;

    /// <summary>
    /// Status: 'O' = Orçamento, 'P' = Pré-Venda / Pedido de Venda, 'V' / 'F' = Venda Efetivada / Finalizada
    /// </summary>
    public string Status { get; set; } = "O";

    /// <summary>
    /// Venda: 0 = Pré-Venda / Orçamento, 1 = Venda Finalizada
    /// </summary>
    public int Venda { get; set; } = 0;
    public int Cancelada { get; set; } = 0;
    public int? LkUsuario { get; set; }
    public int LkCaixa { get; set; } = 1;
    public int IdEmpresa { get; set; } = 1;
    public string? Codigo { get; set; }

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

    public ObservableCollection<PedidoItem> Itens { get; set; } = new();
    public List<FormaPagamento> Pagamentos { get; set; } = new();

    public decimal Entrada { get; set; } = 0m;
    public string? Nota { get; set; }

    public bool IsOrcamento => Status == "O";
    public bool IsVenda => Status == "P" || Status == "V" || Venda == 1;

    /// <summary>
    /// Operação / Tipo do documento conforme regra do ERP Legado:
    /// 'O' = ORÇAMENTO, 'P' = PRÉ-VENDA, 'V' / Venda=1 = VENDA, 'C' / Cancelada=1 = CANCELADO
    /// </summary>
    public string OperacaoDescricao
    {
        get
        {
            if (Cancelada == 1 || Status == "C") return "CANCELADO";
            if (Status == "V" || Venda == 1) return "VENDA";
            if (Status == "P") return "PRÉ-VENDA";
            if (Status == "O") return "ORÇAMENTO";
            return "ORÇAMENTO";
        }
    }

    /// <summary>
    /// Situação operacional conforme regra do ERP Legado:
    /// - PENDENTE: Orçamento ou Pré-Venda ativo aguardando faturamento/aprovação (Subtotal > 0).
    /// - FECHADO: Orçamento ou Pré-Venda já baixado/concretizado pelo caixa (Subtotal == 0 e Entrada/Total > 0).
    /// - CONCLUIDA: Venda concretizada pelo caixa (Status 'V' ou Venda=1).
    /// - CANCELADA: Pedido/Orçamento cancelado.
    /// </summary>
    public string SituacaoDescricao
    {
        get
        {
            if (Cancelada == 1 || Status == "C") return "CANCELADA";
            if (Status == "V" || Venda == 1) return "CONCLUIDA";
            if ((Status == "O" || Status == "P") && Subtotal == 0 && (Total > 0 || Entrada > 0 || Faturado > 0)) return "FECHADO";
            return "PENDENTE";
        }
    }

    public string StatusDescricao => OperacaoDescricao;

    // Cores de Badge para Operação
    public string OperacaoBgColor => OperacaoDescricao switch
    {
        "ORÇAMENTO" => "#E0F2FE",   // Azul suave
        "PRÉ-VENDA" => "#EEF2FF",   // Roxo/Índigo suave
        "VENDA"     => "#F1F5F9",   // Cinza corporativo suave
        "CANCELADO" => "#FEE2E2",   // Vermelho suave
        _ => "#F1F5F9"
    };

    public string OperacaoFgColor => OperacaoDescricao switch
    {
        "ORÇAMENTO" => "#0284C7",   // Azul Primário
        "PRÉ-VENDA" => "#4F46E5",   // Índigo
        "VENDA"     => "#0B192C",   // Navy Escuro
        "CANCELADO" => "#DC2626",   // Vermelho
        _ => "#0B192C"
    };

    // Cores de Badge para Situação
    public string SituacaoBgColor => SituacaoDescricao switch
    {
        "PENDENTE"  => "#FEF3C7",   // Âmbar suave
        "CONCLUIDA" => "#DCFCE7",   // Verde suave
        "FECHADO"   => "#F1F5F9",   // Cinza suave
        "CANCELADA" => "#FEE2E2",   // Vermelho suave
        _ => "#F1F5F9"
    };

    public string SituacaoFgColor => SituacaoDescricao switch
    {
        "PENDENTE"  => "#B45309",   // Âmbar escuro
        "CONCLUIDA" => "#15803D",   // Verde escuro
        "FECHADO"   => "#475569",   // Slate escuro
        "CANCELADA" => "#DC2626",   // Vermelho
        _ => "#475569"
    };

    public string NumeroFormatado => Pedido > 0 ? $"Nº {Pedido}" : "Novo Pedido";
    public string DataFormatada => Data.ToString("dd/MM/yyyy");
    public string NotaFormatada => !string.IsNullOrWhiteSpace(Nota) ? $"NF: {Nota}" : "-";
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
