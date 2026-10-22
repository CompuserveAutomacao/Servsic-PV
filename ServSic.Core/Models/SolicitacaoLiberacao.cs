using System;

namespace ServSic.Core.Models;

public class SolicitacaoLiberacao
{
    public int Controle { get; set; }
    public DateTime DataHora { get; set; } = DateTime.Now;
    public string Terminal { get; set; } = Environment.MachineName;
    public int LkUsuario { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public int? LkVendedor { get; set; }
    public string? NomeVendedor { get; set; }
    public int? LkCliente { get; set; }
    public string? NomeCliente { get; set; }
    public int? LkPedido { get; set; }
    public string Tipo { get; set; } = "DESCONTO"; // "ESTOQUE" ou "DESCONTO"
    public string Descricao { get; set; } = string.Empty;
    public string? Detalhes { get; set; }
    
    public decimal? ValorOriginal { get; set; }
    public decimal? ValorSolicitado { get; set; }
    public decimal? PercentualDesconto { get; set; }
    public int? LkProduto { get; set; }
    public decimal? QtdSolicitada { get; set; }
    public decimal? EstoqueAtual { get; set; }

    public string Status { get; set; } = "P"; // P=Pendente, A=Aprovada, R=Recusada, C=Cancelada
    public int? LkSupervisor { get; set; }
    public string? NomeSupervisor { get; set; }
    public DateTime? DataHoraResposta { get; set; }
    public string? MotivoRecusa { get; set; }

    public bool IsPendente => Status == "P";
    public bool IsAprovada => Status == "A";
    public bool IsRecusada => Status == "R";
    public bool IsCancelada => Status == "C";

    public string StatusDescricao => Status switch
    {
        "P" => "Pendente",
        "A" => "Aprovada",
        "R" => "Recusada",
        "C" => "Cancelada",
        _ => Status
    };

    public string TipoDescricao => Tipo switch
    {
        "ESTOQUE" => "Estoque Insuficiente",
        "DESCONTO" => "Desconto Excedente",
        "AMBOS" => "Desconto e Estoque",
        _ => Tipo
    };

    public string TipoIcone => Tipo switch
    {
        "ESTOQUE" => "Box",
        "DESCONTO" => "Tag",
        _ => "Alert"
    };

    public string ControleFormatado => $"SOLICITAÇÃO #{Controle}";

    public string PedidoExibicao => LkPedido.HasValue && LkPedido.Value > 0
        ? $"Pedido #{LkPedido.Value}"
        : "Balcão / Pré-Venda (Em Aberto)";

    public string ClienteDescricao
    {
        get
        {
            if (LkCliente.HasValue && !string.IsNullOrWhiteSpace(NomeCliente))
                return $"{LkCliente.Value:D6} - {NomeCliente}";
            if (!string.IsNullOrWhiteSpace(NomeCliente))
                return NomeCliente;
            return "Consumidor Final";
        }
    }

    public string OperadorDescricao
    {
        get
        {
            string op = string.IsNullOrWhiteSpace(NomeUsuario) ? "Operador" : NomeUsuario;
            string vend = !string.IsNullOrWhiteSpace(NomeVendedor) 
                ? NomeVendedor 
                : (LkVendedor.HasValue ? $"Vendedor #{LkVendedor}" : "");
            
            if (!string.IsNullOrWhiteSpace(vend) && !vend.Equals(op, StringComparison.OrdinalIgnoreCase))
                return $"{op} (Vend: {vend})";
            return op;
        }
    }
    public string AvaliadorDescricao => !string.IsNullOrWhiteSpace(NomeSupervisor) ? $"Avaliado por: {NomeSupervisor}" : "";
    public string DataHoraFormatada => DataHora.ToString("dd/MM/yyyy HH:mm:ss");
    public string DataHoraRespostaFormatada => DataHoraResposta.HasValue ? $"Data/Hora: {DataHoraResposta.Value:dd/MM/yyyy HH:mm:ss}" : "";
    public string EstoqueAtualFormatado => EstoqueAtual.HasValue ? $"{EstoqueAtual.Value:N2} UN" : "";
    public string QtdSolicitadaFormatada => QtdSolicitada.HasValue ? $"{QtdSolicitada.Value:N2} UN" : "";
    public string PercentualDescontoFormatado => PercentualDesconto.HasValue ? $"{PercentualDesconto.Value:N2}%" : "";
    public string MotivoRecusaFormatado => !string.IsNullOrWhiteSpace(MotivoRecusa) ? $"Motivo da Recusa: {MotivoRecusa}" : "";
    public string ImpactoEstoqueDescricao => EstoqueAtual.HasValue ? (EstoqueAtual.Value <= 0 ? "Sem Saldo em Estoque" : $"Saldo Disponível: {EstoqueAtual.Value:N2} UN") : "";

    public string TempoDecorrido
    {
        get
        {
            var diff = DateTime.Now - DataHora;
            if (diff.TotalSeconds < 60)
                return $"Há {(int)diff.TotalSeconds}s";
            if (diff.TotalMinutes < 60)
                return $"Há {(int)diff.TotalMinutes} min";
            return DataHora.ToString("HH:mm:ss");
        }
    }
}
