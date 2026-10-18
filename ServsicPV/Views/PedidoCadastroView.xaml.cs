using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using ServSic.Core.Models;

namespace ServsicPV.Views;

public partial class PedidoCadastroView : UserControl
{
    public PedidoCadastroView()
    {
        this.InitializeComponent();
        this.DataContextChanged += PedidoCadastroView_DataContextChanged;
        this.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(PedidoCadastroView_KeyDown), handledEventsToo: true);
    }

    private void PedidoCadastroView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is PedidoViewModel vm)
        {
            vm.OnClienteDefinido = () => FocarCodigoProduto();
            vm.OnProdutoDefinido = () => FocarQuantidade();
            vm.OnItemAdicionado = () =>
            {
                FocarCodigoProduto();
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(60);
                    if (vm.Pedido?.Itens?.Count > 0)
                    {
                        ListaItens?.ScrollIntoView(vm.Pedido.Itens[^1]);
                    }
                });
            };
            vm.OnItemAdicaoFalhou = () => FocarDescontoItem();
            vm.OnCheckoutAberto = () => FocarCheckoutValor();

            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PedidoViewModel.IsConfirmarAlteracaoTipoOpen))
                {
                    if (vm.IsConfirmarAlteracaoTipoOpen)
                    {
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            await Task.Delay(40);
                            BtnConfirmarTipo?.Focus(FocusState.Programmatic);
                        });
                    }
                    else
                    {
                        FocarCodigoProduto();
                    }
                }
                else if (e.PropertyName == nameof(PedidoViewModel.IsCheckoutOpen))
                {
                    if (vm.IsCheckoutOpen)
                    {
                        FocarCheckoutValor();
                    }
                    else
                    {
                        FocarCodigoProduto();
                    }
                }
            };
        }
    }

    public void FocarCodigoProduto()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(60);
            TxtCodigoProduto?.Focus(FocusState.Programmatic);
            TxtCodigoProduto?.SelectAll();
        });
    }

    public void FocarQuantidade()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(80);
            TxtQuantidade?.Focus(FocusState.Programmatic);
            TxtQuantidade?.SelectAll();
        });
    }

    public void FocarDescontoItemPercentual()
    {
        FocarDescontoItem();
    }

    public void FocarDescontoItem()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(60);
            TxtDescontoItem?.Focus(FocusState.Programmatic);
            TxtDescontoItem?.SelectAll();
        });
    }

    public void FocarBotaoInserir()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(60);
            BtnInserirItem?.Focus(FocusState.Programmatic);
        });
    }

    public void FocarCheckoutValor()
    {
        CheckoutModal?.FocarValor();
    }

    private void DecimalOnly_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        if (string.IsNullOrEmpty(args.NewText))
            return;

        // Permite apenas números e no máximo uma vírgula ou ponto decimal
        if (!System.Text.RegularExpressions.Regex.IsMatch(args.NewText, @"^[0-9]*[,\.]?[0-9]*$"))
        {
            args.Cancel = true;
        }
    }

    private void PedidoCadastroView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            // Se o modal de confirmação de tipo estiver aberto, atalhos prioritários Enter / Esc
            if (vm.IsConfirmarAlteracaoTipoOpen)
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    vm.ConfirmarAlteracaoTipo();
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    vm.CancelarAlteracaoTipo();
                    e.Handled = true;
                    return;
                }
            }

            // Se o modal de checkout de condições de pagamento estiver aberto
            if (vm.IsCheckoutOpen)
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    vm.FecharCheckout();
                    e.Handled = true;
                    return;
                }
            }

            // Se o modal de cadastro rápido estiver aberto, Esc fecha
            if (vm.IsCadastroRapidoClienteOpen && e.Key == Windows.System.VirtualKey.Escape)
            {
                vm.IsCadastroRapidoClienteOpen = false;
                e.Handled = true;
                return;
            }

            if (e.Key == Windows.System.VirtualKey.F3)
            {
                if (vm.BuscarClienteRapidoCommand.CanExecute(null))
                {
                    vm.BuscarClienteRapidoCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F4)
            {
                if (vm.AbrirConsultaProdutoCommand.CanExecute(null))
                {
                    vm.AbrirConsultaProdutoCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F2)
            {
                if (vm.NovoCommand.CanExecute(null))
                {
                    vm.NovoCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F6 || e.Key == Windows.System.VirtualKey.F10)
            {
                if (!vm.IsCheckoutOpen && !vm.IsConfirmarAlteracaoTipoOpen && !vm.IsCadastroRapidoClienteOpen && !vm.IsConsultaClienteOpen)
                {
                    if (vm.SalvarCommand.CanExecute(null))
                    {
                        vm.SalvarCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }
    }

    private async void CodigoProduto_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.F4)
        {
            if (this.DataContext is PedidoViewModel vm && vm.AbrirConsultaProdutoCommand.CanExecute(null))
            {
                vm.AbrirConsultaProdutoCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (this.DataContext is PedidoViewModel vm)
            {
                string codigoDigitado = TxtCodigoProduto?.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(codigoDigitado))
                {
                    vm.AbrirConsultaProduto();
                }
                else
                {
                    vm.CodigoProdutoInput = codigoDigitado;
                    await vm.BuscarProdutoRapidoAsync();
                    if (vm.ProdutoSelecionado != null)
                    {
                        FocarQuantidade();
                    }
                }
            }
        }
    }

    private void Quantidade_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (this.DataContext is PedidoViewModel vm)
            {
                vm.QuantidadeTexto = TxtQuantidade?.Text?.Trim() ?? "1";
            }
            FocarDescontoItem();
        }
    }

    private void DescontoItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (this.DataContext is PedidoViewModel vm)
            {
                vm.DescontoItemInputTexto = TxtDescontoItem?.Text?.Trim() ?? "0";
            }
            FocarBotaoInserir();
        }
    }

    private void BtnInserirItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            ExecutarInserirItem();
        }
    }

    private void BtnInserirItem_Click(object sender, RoutedEventArgs e)
    {
        ExecutarInserirItem();
    }

    private void ExecutarInserirItem()
    {
        if (this.DataContext is PedidoViewModel vm && vm.HasProdutoSelecionado)
        {
            vm.QuantidadeTexto = TxtQuantidade?.Text?.Trim() ?? "1";
            vm.DescontoItemInputTexto = TxtDescontoItem?.Text?.Trim() ?? "0";
            vm.AdicionarItem();
        }
    }

    // --- Edição de Desconto do Item já Lançado ---
    private Flyout? _activeItemFlyout;
    private string _modalTipoDesconto = "P"; // P = %, V = R$

    private void BtnAbrirEdicaoDesconto_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Flyout is Flyout flyout)
        {
            _activeItemFlyout = flyout;
            _modalTipoDesconto = "P"; // Sempre inicia em %
            if (btn.DataContext is PedidoItem item)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(50);
                    if (flyout.Content is StackPanel panel)
                    {
                        var txtDesc = panel.FindName("TxtModalDesconto") as TextBox;
                        var btnToggle = panel.FindName("BtnModalToggleTipo") as Button;
                        if (btnToggle != null) btnToggle.Content = "%";
                        if (txtDesc != null)
                        {
                            txtDesc.Text = item.PercentualDesconto.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
                            txtDesc.Focus(FocusState.Programmatic);
                            txtDesc.SelectAll();
                        }
                    }
                });
            }
        }
    }

    private void BtnModalToggleTipo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btnToggle && btnToggle.Parent is Grid grid && grid.Parent is StackPanel panel && btnToggle.DataContext is PedidoItem item)
        {
            var txtDesc = grid.FindName("TxtModalDesconto") as TextBox;
            if (txtDesc == null) return;

            decimal.TryParse(txtDesc.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valorAtual);

            if (_modalTipoDesconto == "P")
            {
                // Alterna de % para R$
                _modalTipoDesconto = "V";
                btnToggle.Content = "R$";
                decimal valorReais = Math.Round((item.SubtotalBruto * valorAtual) / 100m, 2);
                txtDesc.Text = valorReais.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                // Alterna de R$ para %
                _modalTipoDesconto = "P";
                btnToggle.Content = "%";
                decimal perc = item.SubtotalBruto > 0 ? Math.Round((valorAtual / item.SubtotalBruto) * 100m, 2) : 0m;
                txtDesc.Text = perc.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            }

            txtDesc.Focus(FocusState.Programmatic);
            txtDesc.SelectAll();
        }
    }

    private void ModalDesc_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is PedidoItem item && this.DataContext is PedidoViewModel vm)
            {
                SalvarDescontoModal(tb, item, vm);
            }
        }
    }

    private void BtnSalvarDescontoItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PedidoItem item && this.DataContext is PedidoViewModel vm)
        {
            SalvarDescontoModal(btn, item, vm);
        }
    }

    private void SalvarDescontoModal(FrameworkElement sender, PedidoItem item, PedidoViewModel vm)
    {
        StackPanel? panel = sender as StackPanel;
        if (panel == null && sender.Parent is StackPanel p1) panel = p1;
        if (panel == null && sender.Parent is Grid g && g.Parent is StackPanel p2) panel = p2;

        if (panel != null)
        {
            var txtDesc = panel.FindName("TxtModalDesconto") as TextBox;
            if (txtDesc != null)
            {
                decimal.TryParse(txtDesc.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valorInformado);

                decimal valorEmReais;
                if (_modalTipoDesconto == "P")
                {
                    valorEmReais = Math.Round((item.SubtotalBruto * valorInformado) / 100m, 2);
                }
                else
                {
                    valorEmReais = valorInformado;
                }

                bool ok = vm.AtualizarDescontoItem(item, valorEmReais);
                if (ok)
                {
                    _activeItemFlyout?.Hide();
                }
            }
        }
    }

    private void FiltroCliente_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.F3)
        {
            if (this.DataContext is PedidoViewModel vm && vm.BuscarClienteRapidoCommand.CanExecute(null))
            {
                vm.BuscarClienteRapidoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void ModalCadastroRapido_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is PedidoViewModel vm && vm.FecharCadastroRapidoClienteCommand.CanExecute(null))
            {
                vm.FecharCadastroRapidoClienteCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void ModalConfirmarTipo_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                vm.ConfirmarAlteracaoTipo();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                vm.CancelarAlteracaoTipo();
                e.Handled = true;
            }
        }
    }

    private void BtnCancelarTipo_Click(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            vm.CancelarAlteracaoTipo();
        }
    }

    private void BtnCancelarTipo_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (this.DataContext is PedidoViewModel vm && vm.IsConfirmarAlteracaoTipoOpen)
        {
            vm.CancelarAlteracaoTipo();
            args.Handled = true;
        }
    }

    private void BtnConfirmarTipo_Click(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            vm.ConfirmarAlteracaoTipo();
        }
    }

    private void BtnConfirmarTipo_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (this.DataContext is PedidoViewModel vm && vm.IsConfirmarAlteracaoTipoOpen)
        {
            vm.ConfirmarAlteracaoTipo();
            args.Handled = true;
        }
    }

    private void NovoClienteCnpj_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (this.DataContext is PedidoViewModel vm && vm.IsPessoaJuridica && vm.ConsultarCnpjCommand.CanExecute(null))
            {
                vm.ConsultarCnpjCommand.Execute(null);
            }
        }
    }

    private void BtnRemoverItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PedidoItem item)
        {
            if (this.DataContext is PedidoViewModel vm)
            {
                vm.RemoverItem(item);
            }
        }
    }

    public Action? OnSolicitarHistorico { get; set; }

    private void BtnConsultarHistorico_Click(object sender, RoutedEventArgs e)
    {
        OnSolicitarHistorico?.Invoke();
    }

    #region Máscaras e Proteção de Caracteres no Cadastro Rápido

    private bool _isFormatting = false;

    private void MascaraNumerica_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        // Impede terminantemente a digitação de letras em campos numéricos/com máscara
        foreach (char c in args.NewText)
        {
            if (!char.IsDigit(c) && c != '.' && c != '-' && c != '/' && c != '(' && c != ')' && c != ' ')
            {
                args.Cancel = true;
                return;
            }
        }
    }

    private void Uf_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        // Impede números ou caracteres especiais na UF, aceitando apenas letras
        foreach (char c in args.NewText)
        {
            if (!char.IsLetter(c))
            {
                args.Cancel = true;
                return;
            }
        }
    }

    private void FormatarComMascara(TextBox tb, Func<string, string> formatar)
    {
        if (_isFormatting) return;
        _isFormatting = true;
        try
        {
            int cursor = tb.SelectionStart;
            string textoAtual = tb.Text ?? string.Empty;
            string textoFormatado = formatar(textoAtual);

            if (textoAtual != textoFormatado)
            {
                tb.Text = textoFormatado;
                int novoCursor = cursor + (textoFormatado.Length - textoAtual.Length);
                tb.SelectionStart = Math.Max(0, Math.Min(textoFormatado.Length, novoCursor));
            }
        }
        finally
        {
            _isFormatting = false;
        }
    }

    private void Documento_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        bool isJuridica = (this.DataContext is PedidoViewModel vm) && vm.IsPessoaJuridica;
        FormatarComMascara(sender, texto =>
        {
            string digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (isJuridica || digitos.Length > 11)
            {
                // CNPJ: 00.000.000/0000-00 (máximo 14 dígitos)
                if (digitos.Length > 14) digitos = digitos.Substring(0, 14);
                if (digitos.Length <= 2) return digitos;
                if (digitos.Length <= 5) return $"{digitos[..2]}.{digitos[2..]}";
                if (digitos.Length <= 8) return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..]}";
                if (digitos.Length <= 12) return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..]}";
                return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..]}";
            }
            else
            {
                // CPF: 000.000.000-00 (máximo 11 dígitos)
                if (digitos.Length > 11) digitos = digitos.Substring(0, 11);
                if (digitos.Length <= 3) return digitos;
                if (digitos.Length <= 6) return $"{digitos[..3]}.{digitos[3..]}";
                if (digitos.Length <= 9) return $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..]}";
                return $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}";
            }
        });
    }

    private void Telefone_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        FormatarComMascara(sender, texto =>
        {
            string digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (digitos.Length > 10) digitos = digitos.Substring(0, 10);
            if (digitos.Length == 0) return string.Empty;
            if (digitos.Length <= 2) return $"({digitos}";
            if (digitos.Length <= 6) return $"({digitos[..2]}) {digitos[2..]}";
            return $"({digitos[..2]}) {digitos[2..6]}-{digitos[6..]}";
        });
    }

    private void Celular_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        FormatarComMascara(sender, texto =>
        {
            string digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (digitos.Length > 11) digitos = digitos.Substring(0, 11);
            if (digitos.Length == 0) return string.Empty;
            if (digitos.Length <= 2) return $"({digitos}";
            if (digitos.Length <= 6) return $"({digitos[..2]}) {digitos[2..]}";
            if (digitos.Length <= 10) return $"({digitos[..2]}) {digitos[2..6]}-{digitos[6..]}";
            return $"({digitos[..2]}) {digitos[2..7]}-{digitos[7..]}";
        });
    }

    private void Cep_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        FormatarComMascara(sender, texto =>
        {
            string digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (digitos.Length > 8) digitos = digitos.Substring(0, 8);
            if (digitos.Length <= 5) return digitos;
            return $"{digitos[..5]}-{digitos[5..]}";
        });
    }

    private void Uf_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        FormatarComMascara(sender, texto =>
        {
            string letras = new string(texto.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            if (letras.Length > 2) letras = letras.Substring(0, 2);
            return letras;
        });
    }

    #endregion

    #region Interações Visuais do Botão Totalizar e Modal de Observações

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushVerdeFoco = new(Windows.UI.Color.FromArgb(255, 22, 163, 74));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushAzulNormal = new(Windows.UI.Color.FromArgb(255, 2, 132, 199));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushAzulEscuroPadrao = new(Windows.UI.Color.FromArgb(255, 11, 25, 44)); // #0B192C

    private void BtnTotalizar_GotFocus(object sender, RoutedEventArgs e)
    {
        if (BtnTotalizar?.IsEnabled == true)
        {
            BtnTotalizar.Background = BrushVerdeFoco;
        }
    }

    private void BtnTotalizar_LostFocus(object sender, RoutedEventArgs e)
    {
        if (BtnTotalizar?.IsEnabled == true)
        {
            BtnTotalizar.Background = BrushAzulNormal;
        }
    }

    private void BtnTotalizar_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (BtnTotalizar?.IsEnabled == true)
        {
            BtnTotalizar.Background = BrushVerdeFoco;
        }
    }

    private void BtnTotalizar_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (BtnTotalizar?.IsEnabled == true && BtnTotalizar.FocusState == FocusState.Unfocused)
        {
            BtnTotalizar.Background = BrushAzulNormal;
        }
    }

    private void BtnConsultarPedidos_GotFocus(object sender, RoutedEventArgs e)
    {
        if (BtnConsultarPedidos != null)
        {
            BtnConsultarPedidos.Background = BrushAzulEscuroPadrao;
        }
    }

    private void BtnConsultarPedidos_LostFocus(object sender, RoutedEventArgs e)
    {
        if (BtnConsultarPedidos != null)
        {
            BtnConsultarPedidos.Background = BrushAzulNormal;
        }
    }

    private void BtnConsultarPedidos_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (BtnConsultarPedidos != null)
        {
            BtnConsultarPedidos.Background = BrushAzulEscuroPadrao;
        }
    }

    private void BtnConsultarPedidos_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (BtnConsultarPedidos != null && BtnConsultarPedidos.FocusState == FocusState.Unfocused)
        {
            BtnConsultarPedidos.Background = BrushAzulNormal;
        }
    }

    private void ModalObservacoes_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is PedidoViewModel vm)
            {
                vm.IsObservacoesOpen = false;
                e.Handled = true;
            }
        }
    }

    #endregion
}
