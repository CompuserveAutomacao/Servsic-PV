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

        // F6 (ou F10) finaliza o pedido de venda se quitado
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
