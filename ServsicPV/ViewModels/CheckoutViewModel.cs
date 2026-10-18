using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml.Media;
using ServsicPV.Common;
using ServSic.Core.Models;
using ServSic.Core.Services;

namespace ServsicPV.ViewModels;

public class PreviaParcelaItem
{
    public int Numero { get; set; }
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }

    public string NumeroEVencimento => $"{Numero}ª Parcela ({Vencimento:dd/MM/yyyy})";
    public string ValorFormatado => Valor.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}

public class CheckoutViewModel : ViewModelBase
{
    private readonly FormaPagamentoService _formaPagamentoService;
    private static readonly CultureInfo CulturePtBr = new("pt-BR");

    private PedidoVenda? _pedido;
    private Usuario? _usuarioLogado;
    private FormaPagamentoTipo? _formaSelecionada;
    private decimal _valorInput = 0m;
    private string _valorInputTexto = "0,00";
    private int _numeroParcelas = 1;
    private int _intervaloDias = 30;
    private DateTimeOffset _dataPrimeiroVencimento = DateTimeOffset.Now.AddDays(30);
    private string _codigoAutorizacao = string.Empty;
    private string _mensagem = string.Empty;
    private bool _isBusy;
    private bool _isOpen;
    private decimal _troco = 0m;
    private readonly List<FormaPagamentoTipo> _todasFormas = new();

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public ObservableCollection<FormaPagamentoTipo> FormasDisponiveis { get; } = new();
    public ObservableCollection<FormaPagamento> PagamentosLancados { get; } = new();
    public ObservableCollection<PreviaParcelaItem> PreviaParcelas { get; } = new();

    public PedidoVenda? Pedido
    {
        get => _pedido;
        private set
        {
            if (SetProperty(ref _pedido, value))
            {
                NotificarTotais();
            }
        }
    }

    public Usuario? UsuarioLogado
    {
        get => _usuarioLogado;
        set => SetProperty(ref _usuarioLogado, value);
    }

    public FormaPagamentoTipo? FormaSelecionada
    {
        get => _formaSelecionada;
        set
        {
            if (SetProperty(ref _formaSelecionada, value))
            {
                OnPropertyChanged(nameof(FormaSelecionadaGeraConta));
                OnPropertyChanged(nameof(FormaSelecionadaIsCartaoOuPix));
                OnPropertyChanged(nameof(IsFormaAPrazoSemCliente));
                if (value != null)
                {
                    if (value.GeraConta)
                    {
                        DataPrimeiroVencimento = DateTimeOffset.Now.AddDays(IntervaloDias);
                    }
                    else
                    {
                        DataPrimeiroVencimento = DateTimeOffset.Now;
                        NumeroParcelas = 1;
                    }

                    if (SaldoRestante > 0)
                    {
                        ValorInput = SaldoRestante;
                    }
                    else
                    {
                        ValorInput = 0m;
                    }
                }
                RecalcularTroco();
                RecalcularParcelas();
            }
        }
    }

    public bool FormaSelecionadaGeraConta => FormaSelecionada?.GeraConta == true;
    public bool FormaSelecionadaIsCartaoOuPix => FormaSelecionada != null && (FormaSelecionada.IsCartao || FormaSelecionada.IsPix);
    public bool IsFormaAPrazoSemCliente => FormaSelecionadaGeraConta && (Pedido?.LkCliente == null || Pedido.LkCliente <= 0);

    public decimal ValorInput
    {
        get => _valorInput;
        set
        {
            decimal rounded = Math.Round(value, 2);
            if (SetProperty(ref _valorInput, rounded))
            {
                _valorInputTexto = rounded.ToString("N2", CulturePtBr);
                OnPropertyChanged(nameof(ValorInputTexto));
                RecalcularTroco();
                RecalcularParcelas();
            }
        }
    }

    public string ValorInputTexto
    {
        get => _valorInputTexto;
        set
        {
            if (SetProperty(ref _valorInputTexto, value))
            {
                if (decimal.TryParse(value.Replace(".", ","), NumberStyles.Any, CulturePtBr, out var v))
                {
                    _valorInput = Math.Round(v, 2);
                    OnPropertyChanged(nameof(ValorInput));
                    RecalcularTroco();
                    RecalcularParcelas();
                }
            }
        }
    }

    public int NumeroParcelas
    {
        get => _numeroParcelas;
        set
        {
            if (SetProperty(ref _numeroParcelas, Math.Max(1, Math.Min(36, value))))
            {
                RecalcularParcelas();
            }
        }
    }

    public int IntervaloDias
    {
        get => _intervaloDias;
        set
        {
            if (SetProperty(ref _intervaloDias, Math.Max(1, value)))
            {
                DataPrimeiroVencimento = DateTimeOffset.Now.AddDays(value);
                RecalcularParcelas();
            }
        }
    }

    public DateTimeOffset DataPrimeiroVencimento
    {
        get => _dataPrimeiroVencimento;
        set
        {
            if (SetProperty(ref _dataPrimeiroVencimento, value))
            {
                RecalcularParcelas();
            }
        }
    }

    public string CodigoAutorizacao
    {
        get => _codigoAutorizacao;
        set => SetProperty(ref _codigoAutorizacao, value);
    }

    public string Mensagem
    {
        get => _mensagem;
        set => SetProperty(ref _mensagem, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // Totais calculados
    public decimal TotalPedido => Pedido?.Total ?? 0m;
    public decimal TotalAProcessar => TotalPedido;
    public decimal TotalPago => PagamentosLancados.Sum(p => p.Valor);
    public decimal SaldoRestante => Math.Max(0m, TotalPedido - TotalPago);
    public decimal TotalRestante => SaldoRestante;

    public decimal Troco
    {
        get => _troco;
        private set
        {
            if (SetProperty(ref _troco, value))
            {
                OnPropertyChanged(nameof(TrocoVisivel));
                OnPropertyChanged(nameof(TrocoFormatado));
                OnPropertyChanged(nameof(ValorTroco));
                OnPropertyChanged(nameof(ValorTrocoFormatado));
            }
        }
    }

    public decimal ValorTroco => Troco;
    public bool TrocoVisivel => Troco > 0;
    public bool TemTroco => TrocoVisivel;
    public bool TemSaldoRestante => !TrocoVisivel;
    public bool IsQuitado => TotalPago >= TotalPedido && TotalPedido > 0;
    public bool HasPagamentos => PagamentosLancados.Count > 0;
    public bool HasNoPagamentos => PagamentosLancados.Count == 0;
    public bool IsPagamentoHabilitado => SaldoRestante > 0;

    // Textos formatados
    public string TotalPedidoFormatado => TotalPedido.ToString("C2", CulturePtBr);
    public string TotalAProcessarFormatado => TotalPedidoFormatado;
    public string TotalPagoFormatado => TotalPago.ToString("C2", CulturePtBr);
    public string SaldoRestanteFormatado => SaldoRestante.ToString("C2", CulturePtBr);
    public string TotalRestanteFormatado => SaldoRestanteFormatado;
    public string TrocoFormatado => Troco.ToString("C2", CulturePtBr);
    public string ValorTrocoFormatado => TrocoFormatado;

    public Brush RestanteBrush => SaldoRestante > 0
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68))  // Vermelho Danger
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199)); // Azul ServSic

    public string TituloModal => Pedido?.IsOrcamento == true ? "TOTALIZAR ORÇAMENTO" : "TOTALIZAR PEDIDO DE VENDA";
    public string SubtituloModal => Pedido?.IsOrcamento == true 
        ? "Balcão / Orçamento - Definição do Plano de Contas" 
        : "Balcão / Pré-Venda - Definição do Plano de Contas";
    public string ClienteExibicao => !string.IsNullOrWhiteSpace(Pedido?.Cliente) ? Pedido.Cliente : "Consumidor / Balcão";
    public string TextoBotaoConfirmar => Pedido?.IsOrcamento == true ? "Concluir Orçamento (F6)" : "Concluir Pedido de Venda (F6)";

    // Callbacks do componente independente
    public event Action<List<FormaPagamento>>? PagamentoConfirmado;
    public event Action? CheckoutCancelado;
    public Action? OnSolicitarFocoValor { get; set; }
    public Action? OnSolicitarFocoFinalizar { get; set; }

    // Comandos
    public ICommand SelecionarFormaCommand { get; }
    public ICommand AdicionarPagamentoCommand { get; }
    public ICommand RemoverPagamentoCommand { get; }
    public ICommand FinalizarCheckoutCommand { get; }
    public ICommand CancelarCheckoutCommand { get; }

    public CheckoutViewModel(FormaPagamentoService? formaPagamentoService = null)
    {
        _formaPagamentoService = formaPagamentoService ?? new FormaPagamentoService();

        SelecionarFormaCommand = new RelayCommand<FormaPagamentoTipo>(forma => SelecionarForma(forma));
        AdicionarPagamentoCommand = new RelayCommand(_ => AdicionarPagamento());
        RemoverPagamentoCommand = new RelayCommand<FormaPagamento>(pag => RemoverPagamento(pag));
        FinalizarCheckoutCommand = new RelayCommand(_ => FinalizarCheckout());
        CancelarCheckoutCommand = new RelayCommand(_ => CancelarCheckout());
    }

    public async Task IniciarAsync(PedidoVenda pedido, Usuario? usuario)
    {
        Pedido = pedido;
        UsuarioLogado = usuario;
        Mensagem = string.Empty;
        PagamentosLancados.Clear();
        PreviaParcelas.Clear();
        IsOpen = true;

        try
        {
            IsBusy = true;
            if (_todasFormas.Count == 0)
            {
                var lista = await _formaPagamentoService.ListarAtivasAsync();
                _todasFormas.Clear();
                _todasFormas.AddRange(lista);
            }

            // Regra: Quando não tiver cliente no pedido, oculta formas de pagamento a prazo (GeraConta = true)
            bool temCliente = pedido.LkCliente.HasValue && pedido.LkCliente.Value > 0 && !string.IsNullOrWhiteSpace(pedido.Cliente);
            var formasFiltradas = temCliente 
                ? _todasFormas 
                : _todasFormas.Where(f => !f.GeraConta).ToList();

            FormasDisponiveis.Clear();
            foreach (var f in formasFiltradas)
            {
                FormasDisponiveis.Add(f);
            }

            // Se o pedido já tiver pagamentos pré-gravados, carrega
            if (pedido.Pagamentos.Count > 0)
            {
                foreach (var p in pedido.Pagamentos)
                {
                    PagamentosLancados.Add(p);
                }
            }

            // Seleciona Dinheiro [1] ou primeira forma disponível
            FormaSelecionada = FormasDisponiveis.FirstOrDefault(f => f.Controle == 1) 
                               ?? FormasDisponiveis.FirstOrDefault();

            decimal sugerido = SaldoRestante > 0 ? SaldoRestante : TotalPedido;
            ValorInput = sugerido;
            NumeroParcelas = 1;
            IntervaloDias = 30;
            DataPrimeiroVencimento = DateTimeOffset.Now.AddDays(30);
            CodigoAutorizacao = string.Empty;

            NotificarTotais();
            RecalcularTroco();
            RecalcularParcelas();
            OnSolicitarFocoValor?.Invoke();
        }
        catch (Exception ex)
        {
            Mensagem = $"Erro ao carregar formas de pagamento: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SelecionarForma(FormaPagamentoTipo? forma)
    {
        if (forma == null || !IsPagamentoHabilitado) return;
        FormaSelecionada = forma;
        Mensagem = string.Empty;
        OnSolicitarFocoValor?.Invoke();
    }

    public void SelecionarFormaPorIndice(int atalhoNumero)
    {
        if (!IsPagamentoHabilitado) return;
        var forma = FormasDisponiveis.FirstOrDefault(f => f.AtalhoNumero == atalhoNumero);
        if (forma != null)
        {
            SelecionarForma(forma);
        }
    }

    private void RecalcularTroco()
    {
        if (SaldoRestante == 0)
        {
            Troco = PagamentosLancados.Where(p => p.VlTroco > 0).Sum(p => p.VlTroco);
        }
        else if (FormaSelecionada != null && FormaSelecionada.IsDinheiro)
        {
            Troco = Math.Max(0m, ValorInput - SaldoRestante);
        }
        else
        {
            Troco = 0m;
        }
    }

    private void RecalcularParcelas()
    {
        PreviaParcelas.Clear();
        if (FormaSelecionada == null || !FormaSelecionada.GeraConta || ValorInput <= 0) return;

        int parcelas = Math.Max(1, Math.Min(36, NumeroParcelas));
        int dias = Math.Max(1, IntervaloDias);

        decimal valorParcelaBase = Math.Round(ValorInput / parcelas, 2);
        decimal diferenca = ValorInput - (valorParcelaBase * parcelas);

        for (int i = 1; i <= parcelas; i++)
        {
            decimal valorParcela = (i == parcelas) ? valorParcelaBase + diferenca : valorParcelaBase;
            DateTime vencto = DataPrimeiroVencimento.DateTime.AddDays((i - 1) * dias);

            PreviaParcelas.Add(new PreviaParcelaItem
            {
                Numero = i,
                Vencimento = vencto,
                Valor = valorParcela
            });
        }
    }

    public void AdicionarPagamento()
    {
        if (SaldoRestante <= 0)
        {
            Mensagem = "O valor do pedido já está totalmente quitado. Para finalizar, clique em Concluir (F6).";
            return;
        }

        if (FormaSelecionada == null)
        {
            Mensagem = "Selecione uma forma de pagamento.";
            return;
        }

        if (ValorInput <= 0)
        {
            Mensagem = "Informe um valor maior que zero.";
            return;
        }

        // Para formas que NÃO sejam Dinheiro, não permite valor maior que o saldo restante
        if (!FormaSelecionada.IsDinheiro && ValorInput > SaldoRestante)
        {
            Mensagem = $"Para a forma '{FormaSelecionada.DescricaoFormatada}', o valor recebido ({ValorInput:C2}) não pode ser maior que o saldo restante ({SaldoRestanteFormatado}). Apenas Dinheiro admite troco.";
            return;
        }

        // Bloqueio de forma a prazo sem cliente identificado
        if (IsFormaAPrazoSemCliente)
        {
            Mensagem = "Pagamento a prazo não permitido para Consumidor Final. Identifique o cliente no pedido antes de prosseguir.";
            return;
        }

        int numPedido = Pedido?.Pedido ?? 0;
        string? numDoc = numPedido > 0 ? numPedido.ToString() : null;

        // Se for Dinheiro com troco
        decimal trocoAdicionar = 0m;
        decimal valorEfetivo = ValorInput;
        if (FormaSelecionada.IsDinheiro && ValorInput > SaldoRestante)
        {
            trocoAdicionar = ValorInput - SaldoRestante;
            valorEfetivo = SaldoRestante;
        }

        // Se forma a prazo e parcelas > 1
        if (FormaSelecionada.GeraConta && NumeroParcelas > 1)
        {
            decimal valorBase = Math.Round(valorEfetivo / NumeroParcelas, 2);
            decimal somaBase = valorBase * (NumeroParcelas - 1);
            decimal valorUltima = valorEfetivo - somaBase;
            int dias = Math.Max(1, IntervaloDias);

            for (int i = 1; i <= NumeroParcelas; i++)
            {
                decimal valorParcela = (i == NumeroParcelas) ? valorUltima : valorBase;
                DateTime vencto = DataPrimeiroVencimento.DateTime.AddDays((i - 1) * dias);

                var pag = new FormaPagamento
                {
                    Pedido = numPedido,
                    LkTipo = FormaSelecionada.Controle,
                    Recebimento = $"{FormaSelecionada.Recebimento} ({i}/{NumeroParcelas})",
                    Data = DateTime.Today,
                    Total = TotalPedido,
                    Valor = valorParcela,
                    Parcela = i,
                    Caixa = "LOJA",
                    LkCaixa = 1,
                    Operador = UsuarioLogado?.Nome ?? "LOJA",
                    LkOperador = UsuarioLogado?.Controle ?? 1,
                    GeraConta = true,
                    Vencimento = vencto,
                    LkConta = FormaSelecionada.LkConta,
                    Conta = FormaSelecionada.ContaRec,
                    NumDoc = numDoc,
                    Autorizacao = CodigoAutorizacao?.Trim()
                };
                PagamentosLancados.Add(pag);
            }
        }
        else
        {
            int proximaParcela = FormaSelecionada.GeraConta 
                ? (PagamentosLancados.Count(p => p.LkTipo == FormaSelecionada.Controle) + 1) 
                : 1;

            var pag = new FormaPagamento
            {
                Pedido = numPedido,
                LkTipo = FormaSelecionada.Controle,
                Recebimento = FormaSelecionada.Recebimento,
                Data = DateTime.Today,
                Total = TotalPedido,
                Valor = valorEfetivo,
                Parcela = proximaParcela,
                Caixa = "LOJA",
                LkCaixa = 1,
                Operador = UsuarioLogado?.Nome ?? "LOJA",
                LkOperador = UsuarioLogado?.Controle ?? 1,
                GeraConta = FormaSelecionada.GeraConta,
                Vencimento = FormaSelecionada.GeraConta ? DataPrimeiroVencimento.DateTime : DateTime.Today,
                LkConta = FormaSelecionada.LkConta,
                Conta = FormaSelecionada.ContaRec,
                NumDoc = numDoc,
                Autorizacao = CodigoAutorizacao?.Trim(),
                AtivaTroco = trocoAdicionar > 0 ? 1 : 0,
                VlTroco = trocoAdicionar,
                TipoTroco = trocoAdicionar > 0 ? "DINHEIRO" : null,
                LkTroco = trocoAdicionar > 0 ? 1 : 0
            };
            PagamentosLancados.Add(pag);
        }

        CodigoAutorizacao = string.Empty;
        Mensagem = string.Empty;
        NotificarTotais();
        RecalcularTroco();

        if (SaldoRestante > 0)
        {
            ValorInput = SaldoRestante;
            OnSolicitarFocoValor?.Invoke();
        }
        else
        {
            ValorInput = 0m;
            OnSolicitarFocoFinalizar?.Invoke();
        }
    }

    public void RemoverPagamento(FormaPagamento? pag)
    {
        if (pag != null && PagamentosLancados.Contains(pag))
        {
            PagamentosLancados.Remove(pag);
            NotificarTotais();
            RecalcularTroco();
            ValorInput = SaldoRestante;
            Mensagem = string.Empty;
            OnSolicitarFocoValor?.Invoke();
        }
    }

    public void FinalizarCheckout()
    {
        if (!IsQuitado)
        {
            Mensagem = $"O valor pago ({TotalPagoFormatado}) deve cobrir o total do pedido ({TotalPedidoFormatado}) para finalizar.";
            return;
        }

        var resultado = PagamentosLancados.ToList();
        // Não fecha antecipadamente; o chamador (PedidoViewModel) fechará após a persistência ter sucesso
        PagamentoConfirmado?.Invoke(resultado);
    }

    public void CancelarCheckout()
    {
        IsOpen = false;
        CheckoutCancelado?.Invoke();
    }

    private void NotificarTotais()
    {
        OnPropertyChanged(nameof(TotalPedido));
        OnPropertyChanged(nameof(TotalAProcessar));
        OnPropertyChanged(nameof(TotalPago));
        OnPropertyChanged(nameof(SaldoRestante));
        OnPropertyChanged(nameof(TotalRestante));
        OnPropertyChanged(nameof(Troco));
        OnPropertyChanged(nameof(ValorTroco));
        OnPropertyChanged(nameof(TrocoVisivel));
        OnPropertyChanged(nameof(TemTroco));
        OnPropertyChanged(nameof(TemSaldoRestante));
        OnPropertyChanged(nameof(IsQuitado));
        OnPropertyChanged(nameof(HasPagamentos));
        OnPropertyChanged(nameof(HasNoPagamentos));
        OnPropertyChanged(nameof(IsPagamentoHabilitado));
        OnPropertyChanged(nameof(TotalPedidoFormatado));
        OnPropertyChanged(nameof(TotalAProcessarFormatado));
        OnPropertyChanged(nameof(TotalPagoFormatado));
        OnPropertyChanged(nameof(SaldoRestanteFormatado));
        OnPropertyChanged(nameof(TotalRestanteFormatado));
        OnPropertyChanged(nameof(TrocoFormatado));
        OnPropertyChanged(nameof(ValorTrocoFormatado));
        OnPropertyChanged(nameof(RestanteBrush));
        OnPropertyChanged(nameof(TituloModal));
        OnPropertyChanged(nameof(SubtituloModal));
        OnPropertyChanged(nameof(ClienteExibicao));
        OnPropertyChanged(nameof(TextoBotaoConfirmar));
    }
}
