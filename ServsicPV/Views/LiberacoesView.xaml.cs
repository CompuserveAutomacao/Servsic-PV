using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServSic.Core.Models;
using ServsicPV.ViewModels;

namespace ServsicPV.Views;

public partial class LiberacoesView : UserControl
{
    public LiberacoesView()
    {
        this.InitializeComponent();
        this.Loaded += LiberacoesView_Loaded;
    }

    public LiberacoesViewModel? ViewModel => DataContext as LiberacoesViewModel;

    private void LiberacoesView_Loaded(object sender, RoutedEventArgs e)
    {
        FocarGrade();
    }

    public void FocarGrade()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (ListaPendentes != null && ListaPendentes.IsLoaded)
                {
                    if (ListaPendentes.SelectedIndex < 0 && ListaPendentes.Items.Count > 0)
                    {
                        ListaPendentes.SelectedIndex = 0;
                    }
                    ListaPendentes.Focus(FocusState.Programmatic);
                }
            }
            catch { }
        });
    }

    private void OnF2Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel?.VoltarCommand.Execute(null);
        args.Handled = true;
    }

    private void OnF5Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel?.AtualizarCommand.Execute(null);
        args.Handled = true;
    }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel != null && ViewModel.IsRecusaDialogOpen)
        {
            ViewModel.CancelarRecusaCommand.Execute(null);
            args.Handled = true;
            return;
        }

        ViewModel?.VoltarCommand.Execute(null);
        args.Handled = true;
    }

    private void Lista_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (ViewModel != null && ViewModel.PodeExecutarAcao)
                {
                    ViewModel.AprovarCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.Delete)
            {
                if (ViewModel != null && ViewModel.PodeExecutarAcao)
                {
                    ViewModel.AbrirRecusaCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiberacoesView] Erro no KeyDown da lista: {ex.Message}");
        }
    }
}
