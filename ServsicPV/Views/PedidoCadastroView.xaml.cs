using System;
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
        this.KeyDown += PedidoCadastroView_KeyDown;
    }

    private void PedidoCadastroView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is PedidoViewModel vm)
        {
            vm.OnConsultaClienteAberta = () => FocarGradeOuPesquisa();
            vm.OnClienteDefinido = () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    TxtCodigoProduto?.Focus(FocusState.Programmatic);
                    TxtCodigoProduto?.SelectAll();
                });
            };
        }
    }

    private void FocarGradeOuPesquisa()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(80);
            if (ListaClientesConsulta != null && ListaClientesConsulta.Items.Count > 0)
            {
                if (ListaClientesConsulta.SelectedIndex < 0)
                {
                    ListaClientesConsulta.SelectedIndex = 0;
                }
                ListaClientesConsulta.Focus(FocusState.Programmatic);
            }
            else if (TxtFiltroConsulta != null)
            {
                TxtFiltroConsulta.Focus(FocusState.Programmatic);
                TxtFiltroConsulta.SelectAll();
            }
        });
    }

    private void PedidoCadastroView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.F3)
        {
            if (this.DataContext is PedidoViewModel vm && vm.BuscarClienteRapidoCommand.CanExecute(null))
            {
                vm.BuscarClienteRapidoCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.F2)
        {
            if (this.DataContext is PedidoViewModel vm && vm.NovoCommand.CanExecute(null))
            {
                vm.NovoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void CodigoProduto_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (this.DataContext is PedidoViewModel vm && vm.BuscarProdutoRapidoCommand.CanExecute(null))
            {
                vm.BuscarProdutoRapidoCommand.Execute(null);
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

    private async void FiltroConsulta_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (this.DataContext is PedidoViewModel vm)
            {
                await vm.PesquisarClientesConsultaAsync();
                FocarGradeOuPesquisa();
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Down)
        {
            if (ListaClientesConsulta != null && ListaClientesConsulta.Items.Count > 0)
            {
                if (ListaClientesConsulta.SelectedIndex < 0)
                {
                    ListaClientesConsulta.SelectedIndex = 0;
                }
                ListaClientesConsulta.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is PedidoViewModel vm && vm.FecharConsultaClienteCommand.CanExecute(null))
            {
                vm.FecharConsultaClienteCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void ListaClientesConsulta_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (this.DataContext is PedidoViewModel vm)
            {
                if (ListaClientesConsulta.SelectedItem is Cliente c)
                {
                    vm.ClienteConsultaSelecionado = c;
                }
                else if (vm.ClienteConsultaSelecionado == null && vm.ClientesConsulta.Count > 0)
                {
                    vm.ClienteConsultaSelecionado = vm.ClientesConsulta[0];
                }

                if (vm.ConfirmarSelecaoClienteCommand.CanExecute(null))
                {
                    vm.ConfirmarSelecaoClienteCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is PedidoViewModel vm && vm.FecharConsultaClienteCommand.CanExecute(null))
            {
                vm.FecharConsultaClienteCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void ListaClientesConsulta_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            if (ListaClientesConsulta.SelectedItem is Cliente c)
            {
                vm.ClienteConsultaSelecionado = c;
            }
            if (vm.ConfirmarSelecaoClienteCommand.CanExecute(null))
            {
                vm.ConfirmarSelecaoClienteCommand.Execute(null);
            }
        }
    }

    private void ModalConsulta_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is PedidoViewModel vm && vm.FecharConsultaClienteCommand.CanExecute(null))
            {
                vm.FecharConsultaClienteCommand.Execute(null);
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
}
