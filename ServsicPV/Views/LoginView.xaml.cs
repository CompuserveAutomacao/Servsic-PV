using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using ServSic.Core.Models;

namespace ServsicPV.Views;

public partial class LoginView : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginView()
    {
        this.InitializeComponent();

        // Configura tamanho e ícone da janela
        this.AppWindow.SetIcon("Assets/icoservsicpv.ico");
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(430, 560));

        // Centraliza na tela
        var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            this.AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);

        if (displayArea != null)
        {
            var centerX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - this.AppWindow.Size.Width) / 2;
            var centerY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - this.AppWindow.Size.Height) / 2;
            this.AppWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
        }

        if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        _viewModel = new LoginViewModel();
        this.RootGrid.DataContext = _viewModel;

        _viewModel.OnLoginSuccess = (usuario) =>
        {
            var mainView = new MainView(usuario);
            mainView.Activate();
            this.Close();
        };

        // Foco inicial automático na senha quando a janela for ativada
        this.Activated += (s, e) =>
        {
            TxtPassword?.Focus(FocusState.Programmatic);
        };
    }

    private void CboUsuario_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            TxtPassword.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void TxtPassword_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _viewModel.Password = TxtPassword.Password;
            if (_viewModel.LoginCommand.CanExecute(null))
            {
                _viewModel.LoginCommand.Execute(null);
            }
        }
    }

    private void BtnConfig_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AbrirConfiguracao();
        TxtConfigPassword.Password = _viewModel.ConfigPassword;
    }

    private void BtnFecharConfig_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.FecharConfiguracao();
    }

    private async void BtnTestarConexao_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ConfigPassword = TxtConfigPassword.Password;
        await _viewModel.TestarConexaoConfigAsync();
    }

    private async void BtnSalvarConfig_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ConfigPassword = TxtConfigPassword.Password;
        await _viewModel.SalvarConexaoConfigAsync();
    }
}
