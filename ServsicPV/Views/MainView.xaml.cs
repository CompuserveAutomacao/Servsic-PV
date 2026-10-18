using System;
using Microsoft.UI.Xaml;
using ServsicPV.ViewModels;
using ServSic.Core.Models;
using ServSic.Core.Services;

namespace ServsicPV.Views;

public partial class MainView : Window
{
    private readonly Usuario _usuarioLogado;
    private readonly PedidoCadastroView _cadastroView;
    private readonly PedidoViewModel _pedidoViewModel;
    private readonly PedidoConsultaView _consultaView;
    private readonly PedidoConsultaViewModel _consultaViewModel;
    private readonly PedidoService _pedidoService;

    public MainView(Usuario usuario)
    {
        this.InitializeComponent();

        _usuarioLogado = usuario;
        TxtUsuarioNome.Text = usuario.Nome;
        TxtUsuarioNivel.Text = usuario.NivelDescricao;
        TxtVendedorNome.Text = !string.IsNullOrWhiteSpace(usuario.NomeVendedor)
            ? usuario.NomeVendedor
            : (usuario.LkVendedor.HasValue ? $"Vendedor #{usuario.LkVendedor}" : usuario.Nome);

        // Configura ícone e inicia a janela principal maximizada
        this.AppWindow.SetIcon("Assets/icoservsicpv.ico");

        if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            ShowWindow(hWnd, SW_MAXIMIZE);
        }
        catch { /* Fallback para presenter.Maximize */ }

        _pedidoService = new PedidoService();
        _pedidoViewModel = new PedidoViewModel(_pedidoService);
        _pedidoViewModel.UsuarioLogado = usuario;
        _pedidoViewModel.OnTipoDocumentoAlterado = () =>
        {
            if (ReferenceEquals(ContentArea.Content, _consultaView))
            {
                MostrarCadastro();
            }
        };

        RootGrid.DataContext = _pedidoViewModel;

        _cadastroView = new PedidoCadastroView { DataContext = _pedidoViewModel };
        _cadastroView.OnSolicitarHistorico = () => MostrarConsulta();

        _consultaViewModel = new PedidoConsultaViewModel(_pedidoService);
        _consultaViewModel.OnEditarPedido = (controle) => CarregarPedidoParaEdicao(controle);
        _consultaView = new PedidoConsultaView { DataContext = _consultaViewModel };
        _consultaView.OnVoltarAoPedido = () => MostrarCadastro();

        // Exibe tela de cadastro inicialmente
        MostrarCadastro();
    }

    private void MostrarCadastro()
    {
        ContentArea.Content = _cadastroView;
    }

    private void MostrarConsulta()
    {
        ContentArea.Content = _consultaView;
        _ = _consultaViewModel.PesquisarAsync();
    }

    private async void CarregarPedidoParaEdicao(int controle)
    {
        try
        {
            var p = await _pedidoService.ObterPorIdAsync(controle);
            if (p != null)
            {
                _pedidoViewModel.Pedido = p;
                MostrarCadastro();
            }
        }
        catch (Exception ex)
        {
            _pedidoViewModel.StatusMensagem = $"Erro ao abrir pedido: {ex.Message}";
        }
    }

    private void OnF3GlobalInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_pedidoViewModel.BuscarClienteRapidoCommand.CanExecute(null))
        {
            _pedidoViewModel.BuscarClienteRapidoCommand.Execute(null);
            args.Handled = true;
        }
    }

    private void OnF2GlobalInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_pedidoViewModel.NovoCommand.CanExecute(null))
        {
            _pedidoViewModel.NovoCommand.Execute(null);
            args.Handled = true;
        }
    }

    private void BtnSair_Click(object sender, RoutedEventArgs e)
    {
        var login = new LoginView();
        login.Activate();
        this.Close();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    private const int SW_MAXIMIZE = 3;
}
