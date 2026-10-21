using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;

namespace ServsicPV.Views;

public partial class CheckoutView : UserControl
{
    public CheckoutView()
    {
        this.InitializeComponent();
        this.DataContextChanged += CheckoutView_DataContextChanged;
    }

    private void CheckoutView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is CheckoutViewModel vm)
        {
            vm.OnSolicitarFocoValor = () => FocarValor();
            vm.OnSolicitarFocoFinalizar = () => FocarBotaoFinalizar();
            vm.OnSolicitarFocoFormaPagamento = () => FocarFormaPagamento();
        }
    }

    public void FocarFormaPagamento()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(50);
            CmbFormaPagamento?.Focus(FocusState.Programmatic);
        });
    }

    public void FocarInicial()
    {
        if (this.DataContext is CheckoutViewModel vm && vm.IsQuitado)
        {
            FocarBotaoFinalizar();
        }
        else
        {
            FocarFormaPagamento();
        }
    }

    public void FocarValor()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(50);
            TxtValor?.Focus(FocusState.Programmatic);
            TxtValor?.SelectAll();
        });
    }

    public void FocarBotaoFinalizar()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(50);
            BtnFinalizar?.Focus(FocusState.Programmatic);
        });
    }

    private void CmbFormaPagamento_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not CheckoutViewModel vm) return;

        if (e.Key == Windows.System.VirtualKey.Down)
        {
            if (!CmbFormaPagamento.IsDropDownOpen)
            {
                int nextIndex = CmbFormaPagamento.SelectedIndex + 1;
                if (nextIndex < CmbFormaPagamento.Items.Count)
                {
                    CmbFormaPagamento.SelectedIndex = nextIndex;
                }
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            if (!CmbFormaPagamento.IsDropDownOpen)
            {
                int prevIndex = CmbFormaPagamento.SelectedIndex - 1;
                if (prevIndex >= 0)
                {
                    CmbFormaPagamento.SelectedIndex = prevIndex;
                }
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (CmbFormaPagamento.IsDropDownOpen)
            {
                CmbFormaPagamento.IsDropDownOpen = false;
            }
            FocarValor();
            e.Handled = true;
            return;
        }
    }

    private void CmbFormaPagamento_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (CmbFormaPagamento.IsDropDownOpen)
            {
                CmbFormaPagamento.IsDropDownOpen = false;
            }
            FocarValor();
            e.Handled = true;
        }
    }

    private void CheckoutView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not CheckoutViewModel vm) return;

        // Esc fecha o modal
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            vm.CancelarCheckout();
            e.Handled = true;
            return;
        }

        // F6 (ou F10) finaliza o pedido de venda somente se quitado
        if (e.Key == Windows.System.VirtualKey.F6 || e.Key == Windows.System.VirtualKey.F10)
        {
            if (vm.IsQuitado)
            {
                vm.FinalizarCheckout();
                e.Handled = true;
                return;
            }
        }

        // Atalhos 1..9 para seleção rápida de forma de pagamento
        // Executa se o foco não for edição ativa ou se acompanhado de tecla padrão
        int? atalho = e.Key switch
        {
            Windows.System.VirtualKey.Number1 or Windows.System.VirtualKey.NumberPad1 => 1,
            Windows.System.VirtualKey.Number2 or Windows.System.VirtualKey.NumberPad2 => 2,
            Windows.System.VirtualKey.Number3 or Windows.System.VirtualKey.NumberPad3 => 3,
            Windows.System.VirtualKey.Number4 or Windows.System.VirtualKey.NumberPad4 => 4,
            Windows.System.VirtualKey.Number5 or Windows.System.VirtualKey.NumberPad5 => 5,
            Windows.System.VirtualKey.Number6 or Windows.System.VirtualKey.NumberPad6 => 6,
            Windows.System.VirtualKey.Number7 or Windows.System.VirtualKey.NumberPad7 => 7,
            Windows.System.VirtualKey.Number8 or Windows.System.VirtualKey.NumberPad8 => 8,
            Windows.System.VirtualKey.Number9 or Windows.System.VirtualKey.NumberPad9 => 9,
            _ => null
        };

        // Se o foco estiver no botão ou fora dos campos de digitação numérica de valor
        var foco = FocusManager.GetFocusedElement(this.XamlRoot);
        bool isDigitandoValor = foco is TextBox tb && (tb == TxtValor || tb == TxtParcelas);

        if (atalho.HasValue && !isDigitandoValor)
        {
            vm.SelecionarFormaPorIndice(atalho.Value);
            e.Handled = true;
        }
    }

    private void TxtValor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (this.DataContext is CheckoutViewModel vm)
            {
                if (vm.FormaSelecionadaGeraConta && TxtParcelas != null)
                {
                    TxtParcelas.Focus(FocusState.Programmatic);
                    TxtParcelas.SelectAll();
                }
                else
                {
                    vm.AdicionarPagamento();
                }
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            FocarFormaPagamento();
            e.Handled = true;
        }
    }

    private void TxtParcelas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (this.DataContext is CheckoutViewModel vm)
            {
                vm.AdicionarPagamento();
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            FocarValor();
            e.Handled = true;
        }
    }

    private void BtnFinalizar_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (this.DataContext is CheckoutViewModel vm && vm.IsQuitado)
            {
                vm.FinalizarCheckout();
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            FocarFormaPagamento();
            e.Handled = true;
        }
    }



    private void DecimalOnly_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (string.IsNullOrEmpty(args.NewText)) return;
        // Permite números de qualquer magnitude, aceitando pontos de milhar e vírgula/ponto decimal
        if (!System.Text.RegularExpressions.Regex.IsMatch(args.NewText, @"^[0-9\.,]*$"))
        {
            args.Cancel = true;
        }
    }

    private void TxtValor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is CheckoutViewModel vm && vm.ValorInput > 0)
        {
            vm.FormatarValorInputTexto();
        }
    }

    private void IntegerOnly_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (string.IsNullOrEmpty(args.NewText)) return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(args.NewText, @"^[0-9]*$"))
        {
            args.Cancel = true;
        }
    }
}
