using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;

namespace ServsicPV.Views;

public sealed partial class CupomVisualizacaoView : UserControl
{
    public CupomVisualizacaoView()
    {
        this.InitializeComponent();
    }

    private void UserControl_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (this.DataContext is CupomVisualizacaoViewModel vm && vm.IsOpen)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                vm.Fechar();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                vm.ImprimirCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
