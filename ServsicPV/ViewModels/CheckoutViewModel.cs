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

                    if (SaldoRestante > 0.005m)
                    {
                        ValorInput = SaldoRestante;
                    }
                    else if (PagamentosLancados.Count == 0 && TotalPedido > 0)
                    {
                        ValorInput = TotalPedido;
                    }
                    else
                    {
                        ValorInput = 0m;
                    }
                }
                RecalcularTroco();
                RecalcularParcelas();
                NotificarTotais();
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
            _valorInput = rounded;
            _valorInputTexto = rounded.ToString("N2", CulturePtBr);
            OnPropertyChanged(nameof(ValorInput));
            OnPropertyChanged(nameof(ValorInputTexto));
            RecalcularTroco();
            RecalcularParcelas();
        }
    }

    public string ValorInputTexto
    {
        get => _valorInputTexto;
        set
        {
            _valorInputTexto = value ?? string.Empty;
            OnPropertyChanged(nameof(ValorInputTexto));
            if (TentarConverterValor(value, out var v))
            {
                _valorInput = Math.Round(v, 2);
                OnPropertyChanged(nameof(ValorInput));
                RecalcularTroco();
                RecalcularParcelas();
            }
        }
    }

    public void FormatarValorInputTexto()
    {
        _valorInputTexto = _valorInput.ToString("N2", CulturePtBr);
        OnPropertyChanged(nameof(ValorInputTexto));
    }

    private static bool TentarConverterValor(string? texto, out decimal valor)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        string limpo = texto.Trim().Replace("R$", "").Trim();

        // 1. Formato com milhar e decimal: "109.000,00" ou "1.250,50"
        if (limpo.Contains('.') && limpo.Contains(','))
        {
            string semMilhar = limpo.Replace(".", "");
            if (decimal.TryParse(semMilhar, NumberStyles.Number, CulturePtBr, out valor))
                return true;
        }

        // 2. Apenas vírgula: "109000,00" ou "50,25"
        if (limpo.Contains(',') && !limpo.Contains('.'))
        {
            if (decimal.TryParse(limpo, NumberStyles.Number, CulturePtBr, out valor))
                return true;
        }

        // 3. Apenas ponto: teclado numérico ("109000.50") ou múltiplos pontos de milhar ("1.000.000")
        if (limpo.Contains('.') && !limpo.Contains(','))
        {
            if (limpo.Count(c => c == '.') > 1)
            {
                string semPontos = limpo.Replace(".", "");
                if (decimal.TryParse(semPontos, NumberStyles.Number, CulturePtBr, out valor))
                    return true;
            }

            if (decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out valor))
                return true;
        }

        // 4. Número inteiro simples: "109000"
        if (decimal.TryParse(limpo, NumberStyles.Number, CulturePtBr, out valor))
            return true;

        return false;
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

    // Totais calculados com arredondamento estrito para 2 casas decimais
    public decimal TotalPedido => Pedido != null ? Math.Round(Pedido.Total, 2) : 0m;
    public decimal TotalAProcessar => TotalPedido;
    public decimal TotalPago => Math.Round(PagamentosLancados.Sum(p => p.Valor), 2);
    public decimal SaldoRestante => Math.Max(0m, Math.Round(TotalPedido - TotalPago, 2));
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
    public bool IsQuitado => TotalPedido > 0 && Math.Round(TotalPago, 2) >= (Math.Round(TotalPedido, 2) - 0.005m);
    public bool HasPagamentos => PagamentosLancados.Count > 0;
    public bool HasNoPagamentos => PagamentosLancados.Count == 0;
    public bool IsPagamentoHabilitado => SaldoRestante > 0.005m;

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

            // Recalcula totais do pedido preventivamente
            pedido.RecalcularTotais();

            // Se o pedido já tiver pagamentos pré-gravados, carrega apenas os válidos com Valor > 0
            if (pedido.Pagamentos.Count > 0)
            {
                foreach (var p in pedido.Pagamentos.Where(p => p.Valor > 0))
                {
                    PagamentosLancados.Add(p);
                }
            }

            // Seleciona Dinheiro [1] ou primeira forma disponível
            FormaSelecionada = FormasDisponiveis.FirstOrDefault(f => f.Controle == 1) 
                               ?? FormasDisponiveis.FirstOrDefault();

            decimal sugerido = SaldoRestante > 0.005m ? SaldoRestante : TotalPedido;
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
        // Se o valor do pedido já está quitado e foi acionado Confirmar Valor / Enter novamente,
        // interpreta como intenção de concluir a venda diretamente!
        if (IsQuitado || SaldoRestante <= 0.005m)
        {
            FinalizarCheckout();
            return;
        }

        if (FormaSelecionada == null)
        {
            Mensagem = "Selecione uma forma de pagamento.";
            return;
        }

        // Tenta recuperar do texto digitado no TextBox caso ValorInput esteja zerado
        if (ValorInput <= 0 && !string.IsNullOrWhiteSpace(ValorInputTexto))
        {
            if (TentarConverterValor(ValorInputTexto, out var vRecuperado) && vRecuperado > 0)
            {
                ValorInput = vRecuperado;
            }
        }

        // Se ainda for <= 0 mas houver saldo restante, assume o saldo restante sugerido
        if (ValorInput <= 0)
        {
            if (SaldoRestante > 0.005m)
            {
                ValorInput = SaldoRestante;
            }
            else
            {
                Mensagem = "Informe um valor maior que zero.";
                return;
            }
        }

        decimal saldoRestanteArredondado = Math.Round(SaldoRestante, 2);

        // Para formas que NÃO sejam Dinheiro, não permite valor maior que o saldo restante (+ tolerância de arredondamento)
        if (!FormaSelecionada.IsDinheiro && Math.Round(ValorInput, 2) > saldoRestanteArredondado + 0.005m)
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
        decimal valorEfetivo = Math.Round(ValorInput, 2);
        if (FormaSelecionada.IsDinheiro && valorEfetivo > saldoRestanteArredondado)
        {
            trocoAdicionar = Math.Round(valorEfetivo - saldoRestanteArredondado, 2);
            valorEfetivo = saldoRestanteArredondado;
        }
        else if (!FormaSelecionada.IsDinheiro && valorEfetivo > saldoRestanteArredondado)
        {
            // Clampa pequenos centavos residuais
            valorEfetivo = saldoRestanteArredondado;
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

        if (SaldoRestante > 0.005m)
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
