using Microsoft.UI.Xaml.Controls;

namespace ServsicPV.Views;

public sealed partial class AtualizacaoModalView : UserControl
{
    public AtualizacaoModalView()
    {
        this.InitializeComponent();
    }

    private async void BtnAtualizarAgora_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (this.DataContext is ViewModels.AtualizacaoViewModel vm && vm.CanAtualizar)
        {
            await vm.ExecutarAtualizacaoAsync();
        }
    }
}
