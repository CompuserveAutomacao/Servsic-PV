using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ServSic.Core.Models;

/// <summary>
/// Representa uma parcela ou forma de pagamento vinculada ao pedido na tabela TabEst3C.
/// </summary>
public class FormaPagamento : INotifyPropertyChanged
{
    private string _recebimento = "DINHEIRO";
    private decimal _valor;
    private int _parcela = 1;
    private DateTime _vencimento = DateTime.Today;
    private decimal _vlTroco;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public int Controle { get; set; }
    public int Pedido { get; set; }

    public string Recebimento
    {
        get => _recebimento;
        set
        {
            if (_recebimento != value)
            {
                _recebimento = value;
                OnPropertyChanged();
            }
        }
    }

    public int LkTipo { get; set; } = 1;
    public DateTime Data { get; set; } = DateTime.Today;
    public decimal Total { get; set; }

    public decimal Valor
    {
        get => _valor;
        set
        {
            if (_valor != value)
            {
                _valor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValorFormatado));
            }
        }
    }

    public int Parcela
    {
        get => _parcela;
        set
        {
            if (_parcela != value)
            {
                _parcela = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ParcelaFormatada));
                OnPropertyChanged(nameof(ParcelaDescricao));
            }
        }
    }

    public string Caixa { get; set; } = "LOJA";
    public int LkCaixa { get; set; } = 1;
    public string Operador { get; set; } = "COMPUSERVE";
    public int LkOperador { get; set; } = 1;
    public string Hora { get; set; } = DateTime.Now.ToString("HH:mm");
    public bool GeraConta { get; set; }

    public DateTime Vencimento
    {
        get => _vencimento;
        set
        {
            if (_vencimento != value)
            {
                _vencimento = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VencimentoFormatado));
                OnPropertyChanged(nameof(VencimentoDescricao));
            }
        }
    }

    public int AtivaTroco { get; set; }
    public int LkTroco { get; set; }
    public string? TipoTroco { get; set; }

    public decimal VlTroco
    {
        get => _vlTroco;
        set
        {
            if (_vlTroco != value)
            {
                _vlTroco = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrocoFormatado));
                OnPropertyChanged(nameof(TrocoVisivel));
            }
        }
    }

    public int IdEmpresa { get; set; } = 1;
    public int? LkConta { get; set; }
    public string? Conta { get; set; }
    public string? NumDoc { get; set; }
    public decimal Divida { get; set; }
    public decimal VlFatura { get; set; }
    public string? CAut { get; set; }
    public string? Autorizacao { get => CAut; set => CAut = value; }

    // Helpers de exibição
    public string ValorFormatado => Valor.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    public string VencimentoFormatado => Vencimento.ToString("dd/MM/yyyy");
    public string VencimentoDescricao => $"Venc: {VencimentoFormatado}";
    public string ParcelaFormatada => $"{Parcela}ª Parcela";
    public string ParcelaDescricao => $"Parc. {Parcela}";
    public string TrocoFormatado => VlTroco.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    public bool TrocoVisivel => VlTroco > 0;
}
