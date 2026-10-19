using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using Windows.System;

namespace ServsicPV.Views;

public sealed partial class EnderecoEntregaView : UserControl
{
    public EnderecoEntregaView()
    {
        this.InitializeComponent();
        this.Loaded += EnderecoEntregaView_Loaded;
    }

    private void EnderecoEntregaView_Loaded(object sender, RoutedEventArgs e)
    {
        // Foca no CEP ou campo inicial ao abrir
        TxtCep?.Focus(FocusState.Programmatic);
    }

    private void EnderecoEntregaView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is not EnderecoEntregaViewModel vm) return;

        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            vm.Fechar();
        }
        else if (e.Key == VirtualKey.Enter)
        {
            // Se o foco estiver no CEP, executa busca de CEP
            if (e.OriginalSource is TextBox tb && tb == TxtCep && !string.IsNullOrWhiteSpace(vm.Cep))
            {
                e.Handled = true;
                _ = vm.BuscarCepAsync();
            }
            else
            {
                e.Handled = true;
                _ = vm.ConfirmarAsync();
            }
        }
    }

    private void TxtCep_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && this.DataContext is EnderecoEntregaViewModel vm)
        {
            e.Handled = true;
            _ = vm.BuscarCepAsync();
        }
    }
}
