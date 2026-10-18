using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ServSic.Core.Models;

public class PedidoItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private decimal _quantidade = 1m;
    private decimal _vlUnitario = 0m;
    private decimal _desconto = 0m;
    private decimal _acrescimo = 0m;
    private decimal _total = 0m;

    public int Controle { get; set; }
    public int LkEst3A { get; set; }
    public int Pedido { get; set; }
    public int LkProduto { get; set; }
    public string? CodInterno { get; set; }
    public string? CodBarra { get; set; }
    public string Produto { get; set; } = string.Empty;
    public decimal PerDescMax { get; set; } = 0m;

    public decimal Quantidade
    {
        get => _quantidade;
        set
        {
            if (_quantidade != value)
            {
                _quantidade = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubtotalBruto));
                RecalcularTotal();
            }
        }
    }

    public decimal VlUnitario
    {
        get => _vlUnitario;
        set
        {
            if (_vlUnitario != value)
            {
                _vlUnitario = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubtotalBruto));
                RecalcularTotal();
            }
        }
    }

    public decimal Desconto
    {
        get => _desconto;
        set
        {
            if (_desconto != value)
            {
                _desconto = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DescontoFormatado));
                OnPropertyChanged(nameof(PercentualDesconto));
                OnPropertyChanged(nameof(PercentualDescontoFormatado));
                RecalcularTotal();
            }
        }
    }

    public decimal Acrescimo
    {
        get => _acrescimo;
        set
        {
            if (_acrescimo != value)
            {
                _acrescimo = value;
                OnPropertyChanged();
                RecalcularTotal();
            }
        }
    }

    public decimal Total
    {
        get => _total;
        set
        {
            if (_total != value)
            {
                _total = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalFormatado));
            }
        }
    }

    public decimal PrecoVenda { get; set; } = 0m;
    public decimal PrCusto { get; set; } = 0m;
    public string? Unidade { get; set; } = "UN";
    public string? Marca { get; set; }
    public int? LkVendedor { get; set; }
    public string? Vendedor { get; set; }

    public string CodigoExibicao => !string.IsNullOrWhiteSpace(CodInterno) 
        ? CodInterno 
        : (!string.IsNullOrWhiteSpace(CodBarra) ? CodBarra : (LkProduto > 0 ? LkProduto.ToString() : ""));

    public decimal SubtotalBruto => Quantidade * VlUnitario;
    public decimal PercentualDesconto => SubtotalBruto > 0 ? Math.Round((Desconto / SubtotalBruto) * 100m, 2) : 0m;
    public string PercentualDescontoFormatado => $"{PercentualDesconto:N2}%";
    public string PerDescMaxFormatado => $"{PerDescMax:N2}%";
    public string DescontoFormatado => $"{Desconto:C2}";
    public string TotalFormatado => $"{Total:C2}";

    public void RecalcularTotal()
    {
        Total = SubtotalBruto - Desconto + Acrescimo;
        if (Total < 0) Total = 0;
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalFormatado));
    }
}
