using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using ServSic.Core.Models;

namespace ServsicPV.Views;

public partial class ClienteConsultaView : UserControl
{
    public ClienteConsultaView()
    {
        this.InitializeComponent();
        this.DataContextChanged += ClienteConsultaView_DataContextChanged;
        this.Loaded += ClienteConsultaView_Loaded;
        this.KeyDown += ClienteConsultaView_KeyDown;

        ListaClientes.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(ListaClientes_KeyDown), handledEventsToo: true);
    }

    private void ClienteConsultaView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is ClienteConsultaViewModel vm)
        {
            vm.OnPesquisaConcluida = () =>
            {
                // Se a grade tiver itens e nenhum selecionado, seleciona o primeiro
                if (ListaClientes.Items.Count > 0 && ListaClientes.SelectedIndex < 0)
                {
                    ListaClientes.SelectedIndex = 0;
                }
            };
        }
    }

    private void ClienteConsultaView_Loaded(object sender, RoutedEventArgs e)
    {
        FocarBarraPesquisa();
    }

    public void FocarBarraPesquisa()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(60);
            TxtFiltro?.Focus(FocusState.Programmatic);
            TxtFiltro?.SelectAll();
        });
    }

    private void ClienteConsultaView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is ClienteConsultaViewModel vm)
            {
                vm.VoltarAoPedidoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void TxtFiltro_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not ClienteConsultaViewModel vm) return;

        if (e.Key == Windows.System.VirtualKey.Down)
        {
            if (ListaClientes.Items.Count > 0)
            {
                if (ListaClientes.SelectedIndex < 0) ListaClientes.SelectedIndex = 0;
                ListaClientes.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (vm.ClienteSelecionado != null)
            {
                vm.ConfirmarSelecao();
            }
            else if (ListaClientes.Items.Count > 0)
            {
                ListaClientes.SelectedIndex = 0;
                vm.ConfirmarSelecao();
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            vm.VoltarAoPedidoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ListaClientes_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not ClienteConsultaViewModel vm) return;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var cliente = (ListaClientes.SelectedItem as Cliente)
                ?? vm.ClienteSelecionado
                ?? (ListaClientes.Items.Count > 0 ? ListaClientes.Items[0] as Cliente : null);

            if (cliente != null)
            {
                vm.ClienteSelecionado = cliente;
                vm.ConfirmarSelecao();
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            vm.VoltarAoPedidoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Up && ListaClientes.SelectedIndex == 0)
        {
            TxtFiltro?.Focus(FocusState.Programmatic);
            TxtFiltro?.SelectAll();
            e.Handled = true;
        }
    }

    private void ListaClientes_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (this.DataContext is ClienteConsultaViewModel vm)
        {
            var cliente = (ListaClientes.SelectedItem as Cliente) ?? vm.ClienteSelecionado;
            if (cliente != null)
            {
                vm.ClienteSelecionado = cliente;
                vm.ConfirmarSelecao();
            }
        }
    }
}
