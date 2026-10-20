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
        this.CharacterReceived += PedidoCadastroView_CharacterReceived;
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
            vm.OnSolicitarFocoSenhaGerente = () => FocarSenhaGerente();

            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PedidoViewModel.IsLiberacaoGerenteOpen))
                {
                    if (vm.IsLiberacaoGerenteOpen)
                    {
                        FocarSenhaGerente();
                    }
                    else
                    {
                        FocarCodigoProduto();
                    }
                }
                else if (e.PropertyName == nameof(PedidoViewModel.IsConfirmarSalvarPendenteOpen))
                {
                    if (vm.IsConfirmarSalvarPendenteOpen)
                    {
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            await Task.Delay(40);
                            BtnConfirmarSalvarPendente?.Focus(FocusState.Programmatic);
                        });
                    }
                    else
                    {
                        FocarCodigoProduto();
                    }
                }
                else if (e.PropertyName == nameof(PedidoViewModel.IsConfirmarAlteracaoTipoOpen))
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

    public void FocarSenhaGerente()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(50);
            TxtSenhaGerente?.Focus(FocusState.Programmatic);
            TxtSenhaGerente?.SelectAll();
        });
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
            // Se o modal de liberação de gerente estiver aberto
            if (vm.IsLiberacaoGerenteOpen)
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    vm.ConfirmarLiberacaoGerenteCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    vm.CancelarLiberacaoGerenteCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            // Se o modal de confirmação de salvar pendente estiver aberto
            if (vm.IsConfirmarSalvarPendenteOpen)
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    vm.ConfirmarSalvarPendenteCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Windows.System.VirtualKey.Delete)
                {
                    vm.DescartarEContinuarCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    vm.CancelarSalvarPendenteCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

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

            // Atalho Ctrl+F: Abrir Consulta de Pedidos
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            bool isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            if (isCtrlPressed && e.Key == Windows.System.VirtualKey.F)
            {
                OnSolicitarHistorico?.Invoke();
                e.Handled = true;
                return;
            }

            // Atalho Ctrl+P: Se venda estiver concluída / bloqueada, reimprimir comprovante
            if (isCtrlPressed && e.Key == Windows.System.VirtualKey.P)
            {
                if (vm.IsEdicaoBloqueada && vm.ReimprimirComprovanteCommand.CanExecute(null))
                {
                    vm.ReimprimirComprovanteCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            // Atalho Ctrl+E: Abrir Endereço de Entrega
            if (isCtrlPressed && e.Key == Windows.System.VirtualKey.E)
            {
                if (!vm.IsEdicaoBloqueada && vm.AbrirEnderecoEntregaCommand.CanExecute(null))
                {
                    vm.AbrirEnderecoEntregaCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Windows.System.VirtualKey.F3)
            {
                if (!vm.IsEdicaoBloqueada && vm.BuscarClienteRapidoCommand.CanExecute(null))
                {
                    vm.BuscarClienteRapidoCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F4)
            {
                if (!vm.IsEdicaoBloqueada && vm.AbrirConsultaProdutoCommand.CanExecute(null))
                {
                    vm.AbrirConsultaProdutoCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F7)
            {
                if (vm.NovoCommand.CanExecute(null))
                {
                    vm.NovoCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F9)
            {
                if (!vm.IsEdicaoBloqueada && vm.AlternarTipoCommand.CanExecute(null))
                {
                    vm.AlternarTipoCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.F6 || e.Key == Windows.System.VirtualKey.F10)
            {
                if (!TemModalAberto(vm))
                {
                    if (vm.IsEdicaoBloqueada)
                    {
                        if (vm.ReimprimirComprovanteCommand.CanExecute(null))
                        {
                            vm.ReimprimirComprovanteCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                    else if (vm.SalvarCommand.CanExecute(null))
                    {
                        vm.SalvarCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
            else
            {
                // Detecção Global de Digitação (Quick-Type / Type-to-Search)
                var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
                bool isAltPressed = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

                if (!isCtrlPressed && !isAltPressed && !vm.IsEdicaoBloqueada && !TemModalAberto(vm))
                {
                    object? focused = null;
                    if (this.XamlRoot != null)
                    {
                        focused = FocusManager.GetFocusedElement(this.XamlRoot);
                    }

                    // 1. Se o foco estiver no campo TxtCodigoProduto ("Bipe ou digite...")
                    if (ReferenceEquals(focused, TxtCodigoProduto))
                    {
                        // Se o operador digitar uma letra (A-Z), ele quer buscar pela descrição!
                        if (e.Key >= Windows.System.VirtualKey.A && e.Key <= Windows.System.VirtualKey.Z)
                        {
                            e.Handled = true;
                            char letra = ObterLetraDeVirtualKey(e.Key);
                            TxtCodigoProduto.Text = string.Empty;
                            vm.AbrirConsultaProduto(letra.ToString());
                            return;
                        }
                        // Se for número (0-9), não intercepta: entra no campo normalmente para permitir leitor de código de barras
                    }
                    // 2. Se o foco NÃO estiver em nenhum campo editável (TextBox, PasswordBox, AutoSuggestBox)
                    else if (focused is not TextBox && focused is not PasswordBox && focused is not AutoSuggestBox)
                    {
                        // Se for letra (A-Z): abre a consulta de produtos já com a letra pesquisada!
                        if (e.Key >= Windows.System.VirtualKey.A && e.Key <= Windows.System.VirtualKey.Z)
                        {
                            e.Handled = true;
                            char letra = ObterLetraDeVirtualKey(e.Key);
                            vm.AbrirConsultaProduto(letra.ToString());
                            return;
                        }

                        // Se for número (0-9): encaminha o foco imediatamente para o campo Código/Barra para bipe ou digitação
                        if ((e.Key >= Windows.System.VirtualKey.Number0 && e.Key <= Windows.System.VirtualKey.Number9) ||
                            (e.Key >= Windows.System.VirtualKey.NumberPad0 && e.Key <= Windows.System.VirtualKey.NumberPad9))
                        {
                            e.Handled = true;
                            char digito = ObterDigitoDeVirtualKey(e.Key);
                            TxtCodigoProduto.Text = digito.ToString();
                            TxtCodigoProduto.Focus(FocusState.Programmatic);
                            TxtCodigoProduto.SelectionStart = 1;
                            TxtCodigoProduto.SelectionLength = 0;
                            return;
                        }
                    }
                }
            }
        }
    }

    private void PedidoCadastroView_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (this.DataContext is not PedidoViewModel vm) return;
        if (vm.IsEdicaoBloqueada || TemModalAberto(vm)) return;

        char c = e.Character;
        if (char.IsControl(c) || c == '\r' || c == '\n' || c == '\t' || c == '\b' || c == (char)27)
            return;

        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        bool isCtrl = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
        bool isAlt = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        if (isCtrl || isAlt) return;

        object? focused = null;
        if (this.XamlRoot != null)
        {
            focused = FocusManager.GetFocusedElement(this.XamlRoot);
        }

        // Se estiver em campos de busca de cliente, quantidade, desconto ou outros modais
        if (ReferenceEquals(focused, TxtFiltroCliente) ||
            ReferenceEquals(focused, TxtQuantidade) ||
            ReferenceEquals(focused, TxtDescontoItem) ||
            (focused is TextBox tbOutro && !ReferenceEquals(tbOutro, TxtCodigoProduto)) ||
            focused is PasswordBox ||
            focused is AutoSuggestBox)
        {
            return;
        }

        // Se o foco estiver no campo TxtCodigoProduto e for digitada letra (ex: ç, acentos que passam por CharacterReceived)
        if (ReferenceEquals(focused, TxtCodigoProduto))
        {
            if (char.IsLetter(c))
            {
                e.Handled = true;
                TxtCodigoProduto.Text = string.Empty;
                vm.AbrirConsultaProduto(c.ToString());
            }
            return;
        }

        // Se o foco estiver fora de qualquer campo de texto
        if (char.IsLetter(c))
        {
            e.Handled = true;
            vm.AbrirConsultaProduto(c.ToString());
        }
    }

    private static bool TemModalAberto(PedidoViewModel vm)
    {
        return vm.IsCheckoutOpen ||
               vm.IsConfirmarAlteracaoTipoOpen ||
               vm.IsConfirmarSalvarPendenteOpen ||
               vm.IsCadastroRapidoClienteOpen ||
               vm.IsConsultaClienteOpen ||
               vm.IsLiberacaoGerenteOpen ||
               vm.IsObservacoesOpen ||
               (vm.CupomVisualizacaoViewModel?.IsOpen == true) ||
               (vm.EnderecoEntregaViewModel?.IsOpen == true) ||
               (vm.ConfiguracaoImpressoraViewModel?.IsOpen == true);
    }

    private static char ObterLetraDeVirtualKey(Windows.System.VirtualKey key)
    {
        if (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z)
        {
            return (char)('A' + (key - Windows.System.VirtualKey.A));
        }
        return ' ';
    }

    private static char ObterDigitoDeVirtualKey(Windows.System.VirtualKey key)
    {
        if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
        {
            return (char)('0' + (key - Windows.System.VirtualKey.Number0));
        }
        if (key >= Windows.System.VirtualKey.NumberPad0 && key <= Windows.System.VirtualKey.NumberPad9)
        {
            return (char)('0' + (key - Windows.System.VirtualKey.NumberPad0));
        }
        return ' ';
    }

    private async void CodigoProduto_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vmBloq && vmBloq.IsEdicaoBloqueada)
        {
            return;
        }

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
        if (this.DataContext is PedidoViewModel vm)
        {
            if (vm.IsEdicaoBloqueada || !vm.HasProdutoSelecionado)
                return;

            vm.QuantidadeTexto = TxtQuantidade?.Text?.Trim() ?? "1";
            vm.DescontoItemInputTexto = TxtDescontoItem?.Text?.Trim() ?? "0";
            vm.AdicionarItem();
        }
    }

    // --- Edição de Quantidade do Item já Lançado ---
    private Flyout? _activeQtdFlyout;

    private void BtnAbrirEdicaoQuantidade_Click(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vmBloq && vmBloq.IsEdicaoBloqueada)
            return;

        if (sender is Button btn && btn.Flyout is Flyout flyout)
        {
            _activeQtdFlyout = flyout;
            if (btn.DataContext is PedidoItem item)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(50);
                    if (flyout.Content is StackPanel panel)
                    {
                        var txtQtd = panel.FindName("TxtModalQuantidade") as TextBox;
                        var txtSubtotal = panel.FindName("TxtModalNovoSubtotal") as TextBlock;
                        if (txtQtd != null)
                        {
                            txtQtd.Text = item.Quantidade.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                            txtQtd.Focus(FocusState.Programmatic);
                            txtQtd.SelectAll();
                        }
                        if (txtSubtotal != null)
                        {
                            txtSubtotal.Text = item.SubtotalBruto.ToString("C2");
                        }
                    }
                });
            }
        }
    }

    private void BtnModalDiminuirQtd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PedidoItem)
        {
            var panel = ObterPainelFlyoutQuantidade(fe);
            if (panel != null)
            {
                var txtQtd = panel.FindName("TxtModalQuantidade") as TextBox;
                if (txtQtd != null)
                {
                    decimal.TryParse(txtQtd.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var atual);
                    if (atual > 1m)
                    {
                        atual = Math.Max(1m, atual - 1m);
                        txtQtd.Text = atual.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                        txtQtd.Focus(FocusState.Programmatic);
                        txtQtd.SelectAll();
                    }
                }
            }
        }
    }

    private void BtnModalAumentarQtd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PedidoItem)
        {
            var panel = ObterPainelFlyoutQuantidade(fe);
            if (panel != null)
            {
                var txtQtd = panel.FindName("TxtModalQuantidade") as TextBox;
                if (txtQtd != null)
                {
                    decimal.TryParse(txtQtd.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var atual);
                    atual += 1m;
                    txtQtd.Text = atual.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    txtQtd.Focus(FocusState.Programmatic);
                    txtQtd.SelectAll();
                }
            }
        }
    }

    private void TxtModalQuantidade_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox txtQtd && txtQtd.DataContext is PedidoItem item)
        {
            var panel = ObterPainelFlyoutQuantidade(txtQtd);
            if (panel != null)
            {
                var txtSubtotal = panel.FindName("TxtModalNovoSubtotal") as TextBlock;
                if (txtSubtotal != null)
                {
                    if (decimal.TryParse(txtQtd.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qtdInformada) && qtdInformada > 0)
                    {
                        decimal subtotalPrevisto = Math.Round(qtdInformada * item.VlUnitario, 2);
                        txtSubtotal.Text = subtotalPrevisto.ToString("C2");
                    }
                    else
                    {
                        txtSubtotal.Text = "R$ 0,00";
                    }
                }
            }
        }
    }

    private void ModalQtd_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (sender is TextBox tb && tb.DataContext is PedidoItem item && this.DataContext is PedidoViewModel vm)
            {
                SalvarQuantidadeModal(tb, item, vm);
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            _activeQtdFlyout?.Hide();
            FocarCodigoProduto();
            e.Handled = true;
        }
    }

    private void BtnSalvarQtdItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PedidoItem item && this.DataContext is PedidoViewModel vm)
        {
            SalvarQuantidadeModal(btn, item, vm);
        }
    }

    private void SalvarQuantidadeModal(FrameworkElement sender, PedidoItem item, PedidoViewModel vm)
    {
        var panel = ObterPainelFlyoutQuantidade(sender);
        if (panel != null)
        {
            var txtQtd = panel.FindName("TxtModalQuantidade") as TextBox;
            if (txtQtd != null)
            {
                if (decimal.TryParse(txtQtd.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var novaQtd))
                {
                    if (novaQtd <= 0)
                    {
                        vm.ExibirMensagem("A quantidade deve ser maior que zero.", false, 4000);
                        return;
                    }

                    bool ok = vm.AtualizarQuantidadeItem(item, novaQtd);
                    if (ok)
                    {
                        _activeQtdFlyout?.Hide();
                        FocarCodigoProduto();
                    }
                }
                else
                {
                    vm.ExibirMensagem("Informe uma quantidade válida.", false, 3000);
                }
            }
        }
    }

    private StackPanel? ObterPainelFlyoutQuantidade(FrameworkElement sender)
    {
        if (sender is StackPanel sp) return sp;
        if (sender.Parent is StackPanel p1) return p1;
        if (sender.Parent is Grid g && g.Parent is StackPanel p2) return p2;
        if (sender.Parent is Border b && b.Parent is StackPanel p3) return p3;
        if (sender.Parent is Grid g2 && g2.Parent is Border b2 && b2.Parent is StackPanel p4) return p4;
        return null;
    }

    // --- Edição de Desconto do Item já Lançado ---
    private Flyout? _activeItemFlyout;
    private string _modalTipoDesconto = "P"; // P = %, V = R$

    private void BtnAbrirEdicaoDesconto_Click(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vmBloq && vmBloq.IsEdicaoBloqueada)
            return;

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

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushAzulNormal = new(Windows.UI.Color.FromArgb(255, 2, 132, 199));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushAzulEscuroPadrao = new(Windows.UI.Color.FromArgb(255, 11, 25, 44)); // #0B192C

    private void BtnTotalizar_GotFocus(object sender, RoutedEventArgs e)
    {
        if (BtnTotalizar?.IsEnabled == true)
        {
            BtnTotalizar.Background = BrushAzulEscuroPadrao;
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
            BtnTotalizar.Background = BrushAzulEscuroPadrao;
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

    private void ModalSalvarPendente_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                vm.ConfirmarSalvarPendenteCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Delete)
            {
                vm.DescartarEContinuarCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                vm.CancelarSalvarPendenteCommand.Execute(null);
                e.Handled = true;
            }
        }
    }



    private void ModalLiberacaoGerente_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                vm.ConfirmarLiberacaoGerenteCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                vm.CancelarLiberacaoGerenteCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void TxtSenhaGerente_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                vm.ConfirmarLiberacaoGerenteCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                vm.CancelarLiberacaoGerenteCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void BtnCancelarLiberacao_Click(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            vm.CancelarLiberacaoGerenteCommand.Execute(null);
        }
    }

    private void BtnConfirmarLiberacao_Click(object sender, RoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            vm.ConfirmarLiberacaoGerenteCommand.Execute(null);
        }
    }

    private void BtnAlternarEntregaItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PedidoItem item)
        {
            item.IsEntrega = !item.IsEntrega;
        }
    }

    #endregion
}
