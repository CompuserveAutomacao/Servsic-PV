using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using ServSic.Core.Models;

namespace ServsicPV.Views;

public partial class PedidoConsultaView : UserControl
{
    public PedidoConsultaView()
    {
        this.InitializeComponent();
    }

    private void FiltroTexto_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
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

    public System.Action? OnVoltarAoPedido { get; set; }

    private void BtnVoltar_Click(object sender, RoutedEventArgs e)
    {
        OnVoltarAoPedido?.Invoke();
    }
}
