using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using ServSic.Core.Models;

namespace ServsicPV.Views;

public partial class PedidoConsultaView : UserControl
{
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushAzulEscuroPadrao = new(Windows.UI.Color.FromArgb(255, 11, 25, 44)); // #0B192C
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushAzulNormal = new(Windows.UI.Color.FromArgb(255, 2, 132, 199)); // #0284C7
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushDangerNormal = new(Windows.UI.Color.FromArgb(255, 220, 38, 38)); // #DC2626
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushDangerHover = new(Windows.UI.Color.FromArgb(255, 185, 28, 28)); // #B91C1C

    public PedidoConsultaView()
    {
        this.InitializeComponent();

        // Registra captura de teclado para assegurar que teclar Enter abra o pedido
        this.PreviewKeyDown += PedidoConsultaView_PreviewKeyDown;
        ListaPedidos.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(ListaPedidos_KeyDown), true);
    }

    public void FocarGradeOuPesquisa()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await System.Threading.Tasks.Task.Delay(50);
            if (ListaPedidos?.Items.Count > 0)
            {
                if (ListaPedidos.SelectedIndex < 0) ListaPedidos.SelectedIndex = 0;
                ListaPedidos.Focus(FocusState.Programmatic);
            }
            else
            {
                TxtPesquisa?.Focus(FocusState.Programmatic);
            }
        });
    }

    private void FiltroTexto_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Down)
        {
            if (ListaPedidos?.Items.Count > 0)
            {
                if (ListaPedidos.SelectedIndex < 0) ListaPedidos.SelectedIndex = 0;
                ListaPedidos.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (this.DataContext is PedidoConsultaViewModel vm)
            {
                if (vm.PesquisarCommand.CanExecute(null))
                {
                    vm.PesquisarCommand.Execute(null);
                }
            }
        }
    }

    private void PedidoConsultaView_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is PedidoConsultaViewModel vm && vm.IsConfirmacaoExclusaoOpen)
            {
                vm.IsConfirmacaoExclusaoOpen = false;
                e.Handled = true;
                return;
            }
            OnVoltarAoPedido?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            // Se o diálogo de confirmação estiver aberto, não interfere
            if (this.DataContext is PedidoConsultaViewModel vm && vm.IsConfirmacaoExclusaoOpen)
            {
                return;
            }

            // Se o foco não for o campo de pesquisa nem botão interativo
            var focused = FocusManager.GetFocusedElement(this.XamlRoot);
            if (focused is not TextBox && focused is not Button)
            {
                AbrirPedidoSelecionado();
                e.Handled = true;
            }
        }
    }

    private void ListaPedidos_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            AbrirPedidoSelecionado();
            e.Handled = true;
        }
    }

    private void ListaPedidos_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            AbrirPedidoSelecionado();
            e.Handled = true;
        }
    }

    private void ListaPedidos_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        AbrirPedidoSelecionado();
        e.Handled = true;
    }

    private void AbrirPedidoSelecionado()
    {
        if (this.DataContext is PedidoConsultaViewModel vm)
        {
            var item = ListaPedidos?.SelectedItem as PedidoVenda ?? vm.PedidoSelecionado ?? vm.PedidoDetalhe;
            if (item != null)
            {
                vm.EditarCommand.Execute(item);
            }
        }
    }

    private void BtnAbrirPedido_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PedidoVenda pedido)
        {
            if (this.DataContext is PedidoConsultaViewModel vm)
            {
                vm.EditarCommand.Execute(pedido);
            }
        }
    }

    private void BtnExcluirPedido_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PedidoVenda pedido)
        {
            if (this.DataContext is PedidoConsultaViewModel vm)
            {
                vm.PedidoSelecionado = pedido;
                vm.SolicitarExcluir(pedido);
            }
        }
    }

    public System.Action? OnVoltarAoPedido { get; set; }

    private void BtnVoltar_Click(object sender, RoutedEventArgs e)
    {
        OnVoltarAoPedido?.Invoke();
    }

    #region Interações de Foco e Hover (Diretriz AGENTS.md: Navy #0B192C com Texto Branco)

    private void BtnNavy_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = BrushAzulEscuroPadrao;
        }
    }

    private void BtnNavy_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = BrushAzulEscuroPadrao;
        }
    }

    private void BtnNavy_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = BrushAzulNormal;
        }
    }

    private void BtnNavy_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = BrushAzulEscuroPadrao;
        }
    }

    private void BtnPesquisar_LostFocus(object sender, RoutedEventArgs e)
    {
        if (BtnPesquisarPedidos != null)
        {
            BtnPesquisarPedidos.Background = BrushAzulNormal;
        }
    }

    private void BtnPesquisar_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (BtnPesquisarPedidos != null && BtnPesquisarPedidos.FocusState == FocusState.Unfocused)
        {
            BtnPesquisarPedidos.Background = BrushAzulNormal;
        }
    }

    private void BtnDanger_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = BrushDangerHover;
        }
    }

    private void BtnDanger_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = BrushDangerNormal;
        }
    }

    private void BtnDanger_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Background = BrushDangerHover;
        }
    }

    private void BtnDanger_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button btn && btn.FocusState == FocusState.Unfocused)
        {
            btn.Background = BrushDangerNormal;
        }
    }

    #endregion
}
