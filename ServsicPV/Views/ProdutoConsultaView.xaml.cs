using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using ServSic.Core.Models;

namespace ServsicPV.Views;

public partial class ProdutoConsultaView : UserControl
{
    public ProdutoConsultaView()
    {
        this.InitializeComponent();
        this.DataContextChanged += ProdutoConsultaView_DataContextChanged;
        this.Loaded += ProdutoConsultaView_Loaded;
        this.KeyDown += ProdutoConsultaView_KeyDown;

        ListaProdutos.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(ListaProdutos_KeyDown), handledEventsToo: true);
    }

    private void ProdutoConsultaView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is ProdutoConsultaViewModel vm)
        {
            vm.OnPesquisaConcluida = () =>
            {
                if (ListaProdutos.Items.Count > 0 && ListaProdutos.SelectedIndex < 0)
                {
                    ListaProdutos.SelectedIndex = 0;
                }
            };
        }
    }

    private void ProdutoConsultaView_Loaded(object sender, RoutedEventArgs e)
    {
        FocarBarraPesquisa(true);
    }

    public void FocarBarraPesquisa(bool posicionarNoFim = true)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(30);
            if (TxtFiltro != null)
            {
                TxtFiltro.Focus(FocusState.Programmatic);
                if (!string.IsNullOrEmpty(TxtFiltro.Text))
                {
                    TxtFiltro.SelectionStart = TxtFiltro.Text.Length;
                    TxtFiltro.SelectionLength = 0;
                }
            }
        });
    }

    private void TxtFiltro_GotFocus(object sender, RoutedEventArgs e)
    {
        if (TxtFiltro != null && !string.IsNullOrEmpty(TxtFiltro.Text))
        {
            TxtFiltro.SelectionStart = TxtFiltro.Text.Length;
            TxtFiltro.SelectionLength = 0;
        }
    }

    private void ProdutoConsultaView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is ProdutoConsultaViewModel vm)
            {
                vm.VoltarAoPedidoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void TxtFiltro_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not ProdutoConsultaViewModel vm) return;

        if (e.Key == Windows.System.VirtualKey.Down)
        {
            if (ListaProdutos.Items.Count > 0)
            {
                if (ListaProdutos.SelectedIndex < 0) ListaProdutos.SelectedIndex = 0;
                ListaProdutos.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (vm.FiltroTexto != vm.UltimoFiltroPesquisado)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await vm.PesquisarImediatoAsync();
                    if (vm.Produtos.Count == 1)
                    {
                        vm.ConfirmarSelecao();
                    }
                });
            }
            else if (vm.ProdutoSelecionado != null)
            {
                vm.ConfirmarSelecao();
            }
            else if (ListaProdutos.Items.Count > 0)
            {
                ListaProdutos.SelectedIndex = 0;
                vm.ConfirmarSelecao();
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            vm.VoltarAoPedidoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FiltroAvancado_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not ProdutoConsultaViewModel vm) return;

        if (e.Key == Windows.System.VirtualKey.Down)
        {
            if (ListaProdutos.Items.Count > 0)
            {
                if (ListaProdutos.SelectedIndex < 0) ListaProdutos.SelectedIndex = 0;
                ListaProdutos.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            _ = vm.PesquisarImediatoAsync();
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            vm.VoltarAoPedidoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ListaProdutos_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not ProdutoConsultaViewModel vm) return;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var prod = (ListaProdutos.SelectedItem as Produto)
                ?? vm.ProdutoSelecionado
                ?? (ListaProdutos.Items.Count > 0 ? ListaProdutos.Items[0] as Produto : null);

            if (prod != null)
            {
                vm.ProdutoSelecionado = prod;
                vm.ConfirmarSelecao();
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            vm.VoltarAoPedidoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Up && ListaProdutos.SelectedIndex == 0)
        {
            TxtFiltro?.Focus(FocusState.Programmatic);
            TxtFiltro?.SelectAll();
            e.Handled = true;
        }
    }

    private void ListaProdutos_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (this.DataContext is ProdutoConsultaViewModel vm)
        {
            var prod = (ListaProdutos.SelectedItem as Produto) ?? vm.ProdutoSelecionado;
            if (prod != null)
            {
                vm.ProdutoSelecionado = prod;
                vm.ConfirmarSelecao();
            }
        }
    }
}
