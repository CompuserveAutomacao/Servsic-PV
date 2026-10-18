using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using ServSic.Core.Models;

namespace ServsicPV.Views;

public partial class LoginView : Window
{
    private readonly LoginViewModel _viewModel;

    // PInvoke para permitir arrastar a janela sem barra de título
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

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

        // Remove borda e barra de título tradicional do Windows (modo tela limpa/borderless)
        if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
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

    private void Header_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Permite que o operador mova a tela arrastando o cabeçalho superior escuro
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            ReleaseCapture();
            SendMessage(hWnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
        catch
        {
            // Fallback seguro caso PInvoke não seja permitido
        }
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

    private void BtnSair_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
        Application.Current.Exit();
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (_viewModel.IsConfigOpen)
            {
                _viewModel.FecharConfiguracao();
            }
            else
            {
                this.Close();
                Application.Current.Exit();
            }
            e.Handled = true;
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
