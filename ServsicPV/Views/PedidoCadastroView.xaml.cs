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
        this.KeyDown += PedidoCadastroView_KeyDown;

        // Registra AddHandler com handledEventsToo: true para garantir captura de Enter e Escape na lista
        ListaClientesConsulta.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(ListaClientesConsulta_KeyDown), handledEventsToo: true);
    }

    private void PedidoCadastroView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is PedidoViewModel vm)
        {
            // Ao abrir a janela modal de consulta, o foco DEVE ficar na barra de pesquisa
            vm.OnConsultaClienteAberta = () => FocarBarraPesquisaModal();
            vm.OnClienteDefinido = () => FocarCodigoProduto();
        }
    }

    private void FocarBarraPesquisaModal()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(60);
            if (TxtFiltroConsulta != null)
            {
                TxtFiltroConsulta.Focus(FocusState.Programmatic);
                TxtFiltroConsulta.SelectAll();
            }
        });
    }

    private void FocarGradeClientes()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(40);
            if (ListaClientesConsulta != null && ListaClientesConsulta.Items.Count > 0)
            {
                if (ListaClientesConsulta.SelectedIndex < 0)
                {
                    ListaClientesConsulta.SelectedIndex = 0;
                }
                ListaClientesConsulta.Focus(FocusState.Programmatic);
            }
        });
    }

    private void FocarCodigoProduto()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TxtCodigoProduto?.Focus(FocusState.Programmatic);
            TxtCodigoProduto?.SelectAll();
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
                FocarGradeClientes();
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Down)
        {
            // Seta para baixo na barra de pesquisa desce o foco direto para a grade
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
            if (this.DataContext is PedidoViewModel vm)
            {
                vm.IsConsultaClienteOpen = false;
                e.Handled = true;
            }
        }
    }

    private void ListaClientesConsulta_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Cliente c && this.DataContext is PedidoViewModel vm)
        {
            vm.SelecionarCliente(c);
            vm.IsConsultaClienteOpen = false;
            vm.FiltroCliente = string.Empty;
            FocarCodigoProduto();
        }
    }

    private void ListaClientesConsulta_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not PedidoViewModel vm) return;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var cliente = (ListaClientesConsulta.SelectedItem as Cliente)
                ?? (ListaClientesConsulta.SelectedIndex >= 0 && ListaClientesConsulta.SelectedIndex < vm.ClientesConsulta.Count
                    ? vm.ClientesConsulta[ListaClientesConsulta.SelectedIndex]
                    : vm.ClienteConsultaSelecionado)
                ?? vm.ClientesConsulta.FirstOrDefault();

            if (cliente != null)
            {
                vm.SelecionarCliente(cliente);
                vm.IsConsultaClienteOpen = false;
                vm.FiltroCliente = string.Empty;
                FocarCodigoProduto();
                e.Handled = true;
            }
            else
            {
                // Alternativa: transfere o foco para o botão Selecionar Cliente
                BtnSelecionarCliente?.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            vm.IsConsultaClienteOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Tab)
        {
            BtnSelecionarCliente?.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void ListaClientesConsulta_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (this.DataContext is PedidoViewModel vm)
        {
            var cliente = (ListaClientesConsulta.SelectedItem as Cliente)
                ?? vm.ClienteConsultaSelecionado
                ?? vm.ClientesConsulta.FirstOrDefault();

            if (cliente != null)
            {
                vm.SelecionarCliente(cliente);
                vm.IsConsultaClienteOpen = false;
                vm.FiltroCliente = string.Empty;
                FocarCodigoProduto();
            }
        }
    }

    private void BotoesRodape_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not PedidoViewModel vm) return;

        if (e.Key == Windows.System.VirtualKey.Left)
        {
            BtnCancelarConsulta?.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Right)
        {
            BtnSelecionarCliente?.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            ListaClientesConsulta?.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (ReferenceEquals(sender, BtnSelecionarCliente))
            {
                vm.ConfirmarClienteConsulta();
                FocarCodigoProduto();
            }
            else if (ReferenceEquals(sender, BtnCancelarConsulta))
            {
                vm.IsConsultaClienteOpen = false;
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            vm.IsConsultaClienteOpen = false;
            e.Handled = true;
        }
    }

    private void ModalConsulta_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is PedidoViewModel vm)
            {
                vm.IsConsultaClienteOpen = false;
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
