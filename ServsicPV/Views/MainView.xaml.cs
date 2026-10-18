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
    private readonly ClienteConsultaView _clienteConsultaView;
    private readonly ClienteConsultaViewModel _clienteConsultaViewModel;
    private readonly ProdutoConsultaView _produtoConsultaView;
    private readonly ProdutoConsultaViewModel _produtoConsultaViewModel;
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
            if (ReferenceEquals(ContentArea.Content, _consultaView) 
                || ReferenceEquals(ContentArea.Content, _clienteConsultaView)
                || ReferenceEquals(ContentArea.Content, _produtoConsultaView))
            {
                MostrarCadastro();
            }
        };

        RootGrid.DataContext = _pedidoViewModel;

        _cadastroView = new PedidoCadastroView { DataContext = _pedidoViewModel };
        _cadastroView.OnSolicitarHistorico = () => MostrarConsulta();

        _consultaViewModel = new PedidoConsultaViewModel(_pedidoService) { UsuarioLogado = usuario };
        _consultaViewModel.OnEditarPedido = (controle) => CarregarPedidoParaEdicao(controle);
        _consultaView = new PedidoConsultaView { DataContext = _consultaViewModel };
        _consultaView.OnVoltarAoPedido = () => MostrarCadastro();

        // Tela Cheia Dedicada de Consulta de Clientes (F3)
        _clienteConsultaViewModel = new ClienteConsultaViewModel(new ClienteService());
        _clienteConsultaViewModel.OnClienteSelecionado = (cliente) =>
        {
            _pedidoViewModel.SelecionarCliente(cliente);
            MostrarCadastro();
            _cadastroView.FocarCodigoProduto();
        };
        _clienteConsultaViewModel.OnVoltarAoPedido = () =>
        {
            MostrarCadastro();
            _cadastroView.FocarCodigoProduto();
        };
        _clienteConsultaViewModel.OnSolicitarNovoCliente = () =>
        {
            MostrarCadastro();
            _pedidoViewModel.AbrirCadastroRapido();
        };

        _clienteConsultaView = new ClienteConsultaView { DataContext = _clienteConsultaViewModel };

        _pedidoViewModel.OnSolicitarConsultaCliente = (filtro) =>
        {
            MostrarConsultaCliente(filtro);
        };

        // Tela Cheia Dedicada de Consulta de Produtos (F4 - ITENS)
        _produtoConsultaViewModel = new ProdutoConsultaViewModel(new ProdutoService());
        _produtoConsultaViewModel.OnProdutoSelecionado = (produto) =>
        {
            _pedidoViewModel.SelecionarProdutoDaConsulta(produto);
            MostrarCadastro();
            _cadastroView.FocarQuantidade();
        };
        _produtoConsultaViewModel.OnVoltarAoPedido = () =>
        {
            MostrarCadastro();
            _cadastroView.FocarCodigoProduto();
        };

        _produtoConsultaView = new ProdutoConsultaView { DataContext = _produtoConsultaViewModel };

        _pedidoViewModel.OnSolicitarConsultaProduto = (filtro) =>
        {
            MostrarConsultaProduto(filtro);
        };

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
        _consultaViewModel.Abrir(_pedidoViewModel.UsuarioLogado);
        _consultaView.FocarGradeOuPesquisa();
    }

    private void MostrarConsultaCliente(string? filtro = null)
    {
        ContentArea.Content = _clienteConsultaView;
        _clienteConsultaViewModel.Abrir(filtro);
        _clienteConsultaView.FocarBarraPesquisa();
    }

    private void MostrarConsultaProduto(string? filtro = null)
    {
        ContentArea.Content = _produtoConsultaView;
        _produtoConsultaViewModel.Abrir(filtro);
        _produtoConsultaView.FocarBarraPesquisa();
    }

    private async void CarregarPedidoParaEdicao(int controle)
    {
        try
        {
            var p = await _pedidoService.ObterPorIdAsync(controle);
            if (p != null)
            {
                await _pedidoViewModel.CarregarPedidoAsync(p);
                MostrarCadastro();
                _cadastroView.FocarCodigoProduto();
            }
        }
        catch (Exception ex)
        {
            _pedidoViewModel.StatusMensagem = $"Erro ao abrir pedido: {ex.Message}";
        }
    }

    private void OnF3GlobalInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ReferenceEquals(ContentArea.Content, _clienteConsultaView))
        {
            _clienteConsultaView.FocarBarraPesquisa();
            args.Handled = true;
            return;
        }

        MostrarConsultaCliente(_pedidoViewModel.FiltroCliente);
        args.Handled = true;
    }

    private void OnF4GlobalInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ReferenceEquals(ContentArea.Content, _produtoConsultaView))
        {
            _produtoConsultaView.FocarBarraPesquisa();
            args.Handled = true;
            return;
        }

        MostrarConsultaProduto(_pedidoViewModel.CodigoProdutoInput);
        args.Handled = true;
    }

    private void OnF2GlobalInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_pedidoViewModel.NovoCommand.CanExecute(null))
        {
            _pedidoViewModel.NovoCommand.Execute(null);
            args.Handled = true;
        }
    }

    private void OnEscapeGlobalInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_pedidoViewModel.ConfiguracaoImpressoraViewModel.IsOpen)
        {
            _pedidoViewModel.ConfiguracaoImpressoraViewModel.Fechar();
            args.Handled = true;
            return;
        }

        if (_pedidoViewModel.CupomVisualizacaoViewModel.IsOpen)
        {
            _pedidoViewModel.CupomVisualizacaoViewModel.Fechar();
            args.Handled = true;
            return;
        }

        if (_pedidoViewModel.IsObservacoesOpen)
        {
            _pedidoViewModel.IsObservacoesOpen = false;
            args.Handled = true;
            return;
        }

        if (_pedidoViewModel.IsCheckoutOpen)
        {
            _pedidoViewModel.FecharCheckout();
            args.Handled = true;
            return;
        }

        if (_pedidoViewModel.IsConfirmarAlteracaoTipoOpen)
        {
            _pedidoViewModel.CancelarAlteracaoTipo();
            args.Handled = true;
            return;
        }

        if (_pedidoViewModel.IsCadastroRapidoClienteOpen)
        {
            _pedidoViewModel.IsCadastroRapidoClienteOpen = false;
            args.Handled = true;
            return;
        }

        if (_pedidoViewModel.IsConsultaClienteOpen)
        {
            _pedidoViewModel.IsConsultaClienteOpen = false;
            args.Handled = true;
            return;
        }

        if (ReferenceEquals(ContentArea.Content, _consultaView)
            || ReferenceEquals(ContentArea.Content, _clienteConsultaView)
            || ReferenceEquals(ContentArea.Content, _produtoConsultaView))
        {
            MostrarCadastro();
            _cadastroView.FocarCodigoProduto();
            args.Handled = true;
            return;
        }
    }

    private void BtnConfiguracoes_Click(object sender, RoutedEventArgs e)
    {
        _pedidoViewModel.ConfiguracaoImpressoraViewModel.Abrir();
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
