using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ServsicPV.Common;
using ServSic.Core.Models;
using ServSic.Core.Services;

namespace ServsicPV.ViewModels;

public class PedidoViewModel : ViewModelBase
{
    private readonly PedidoService _pedidoService;
    private readonly ClienteService _clienteService;
    private readonly ProdutoService _produtoService;
    private readonly VendedorService _vendedorService;
    private readonly CnpjLookupService _cnpjLookupService = new();

    private PedidoVenda _pedido = new();
    private Cliente? _selectedCliente;
    private Vendedor? _selectedVendedor;
    private bool _isBusy;
    private string _statusMensagem = string.Empty;
    private bool _isSucesso;
    private string _filtroCliente = string.Empty;

    // Campos de inserção de itens rápidos
    private string _codigoProdutoInput = string.Empty;
    private decimal _quantidadeInput = 1m;
    private decimal _vlUnitarioInput = 0m;
    private decimal _descontoItemInput = 0m;
    private Produto? _produtoSelecionado;

    private Usuario? _usuarioLogado;

    // --- Modal: Consulta de Clientes ---
    private bool _isConsultaClienteOpen;
    private string _filtroConsultaCliente = string.Empty;
    private Cliente? _clienteConsultaSelecionado;
    private bool _isBuscandoClientesConsulta;
    private CancellationTokenSource? _pesquisaClienteCts;

    // --- Modal: Cadastro Rápido de Cliente ---
    private bool _isCadastroRapidoClienteOpen;
    private string _tipoPessoa = "F"; // "F" = Física, "J" = Jurídica
    private string _novoClienteNome = string.Empty;
    private string _novoClienteFantasia = string.Empty;
    private string _novoClienteDocumento = string.Empty;
    private string _novoClienteTelefone = string.Empty;
    private string _novoClienteCelular = string.Empty;
    private string _novoClienteCEP = string.Empty;
    private string _novoClienteEndereco = string.Empty;
    private string _novoClienteNumero = string.Empty;
    private string _novoClienteBairro = string.Empty;
    private string _novoClienteCidade = string.Empty;
    private string _novoClienteUF = string.Empty;
    private string _novoClienteEmail = string.Empty;
    private string _cadastroRapidoMensagem = string.Empty;
    private bool _isConsultandoCnpj;

    public Usuario? UsuarioLogado
    {
        get => _usuarioLogado;
        set
        {
            if (SetProperty(ref _usuarioLogado, value) && value != null)
            {
                Pedido.LkUsuario = value.Controle;
                Pedido.LkVendedor = value.LkVendedor;
                Pedido.Vendedor = !string.IsNullOrWhiteSpace(value.NomeVendedor) ? value.NomeVendedor : value.Nome;
                OnPropertyChanged(nameof(Pedido));
            }
        }
    }

    public PedidoVenda Pedido
    {
        get => _pedido;
        set
        {
            if (SetProperty(ref _pedido, value))
            {
                NotificarAlteracaoTipo();
                OnPropertyChanged(nameof(HasClienteSelecionado));
            }
        }
    }

    public ObservableCollection<Cliente> ClientesFiltrados { get; } = new();
    public ObservableCollection<Cliente> ClientesConsulta { get; } = new();
    public ObservableCollection<Vendedor> Vendedores { get; } = new();
    public ObservableCollection<Produto> ProdutosFiltrados { get; } = new();

    public Cliente? SelectedCliente
    {
        get => _selectedCliente;
        set
        {
            if (SetProperty(ref _selectedCliente, value) && value != null)
            {
                AtribuirClienteAoPedido(value);
            }
            OnPropertyChanged(nameof(HasClienteSelecionado));
        }
    }

    public bool HasClienteSelecionado =>
        SelectedCliente != null || (!string.IsNullOrWhiteSpace(Pedido?.Cliente) && (Pedido?.LkCliente ?? 0) > 0);

    public Vendedor? SelectedVendedor
    {
        get => _selectedVendedor;
        set
        {
            if (SetProperty(ref _selectedVendedor, value) && value != null)
            {
                Pedido.LkVendedor = value.Controle;
                Pedido.Vendedor = value.Nome;
                OnPropertyChanged(nameof(Pedido));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMensagem
    {
        get => _statusMensagem;
        set => SetProperty(ref _statusMensagem, value);
    }

    public bool IsSucesso
    {
        get => _isSucesso;
        set => SetProperty(ref _isSucesso, value);
    }

    public string FiltroCliente
    {
        get => _filtroCliente;
        set => SetProperty(ref _filtroCliente, value);
    }

    public string CodigoProdutoInput
    {
        get => _codigoProdutoInput;
        set => SetProperty(ref _codigoProdutoInput, value);
    }

    public decimal QuantidadeInput
    {
        get => _quantidadeInput;
        set => SetProperty(ref _quantidadeInput, value);
    }

    public decimal VlUnitarioInput
    {
        get => _vlUnitarioInput;
        set => SetProperty(ref _vlUnitarioInput, value);
    }

    public decimal DescontoItemInput
    {
        get => _descontoItemInput;
        set => SetProperty(ref _descontoItemInput, value);
    }

    public Produto? ProdutoSelecionado
    {
        get => _produtoSelecionado;
        set
        {
            if (SetProperty(ref _produtoSelecionado, value) && value != null)
            {
                VlUnitarioInput = value.PrVenda;
                QuantidadeInput = 1m;
                DescontoItemInput = 0m;
            }
        }
    }

    public bool IsOrcamento => Pedido.Status == "O";
    public bool IsVenda => Pedido.Status == "V";
    public string TituloTela => IsOrcamento ? "Orçamento de Venda" : "Pedido de Venda";

    public Microsoft.UI.Xaml.Media.Brush BrushOrcamentoBackground => IsOrcamento
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public Microsoft.UI.Xaml.Media.Brush BrushOrcamentoForeground => IsOrcamento
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139));

    public Microsoft.UI.Xaml.Media.Brush BrushVendaBackground => IsVenda
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public Microsoft.UI.Xaml.Media.Brush BrushVendaForeground => IsVenda
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139));

    public Action? OnTipoDocumentoAlterado { get; set; }
    public Action? OnConsultaClienteAberta { get; set; }
    public Action? OnClienteDefinido { get; set; }

    // --- Propriedades do Modal de Consulta ---
    public bool IsConsultaClienteOpen
    {
        get => _isConsultaClienteOpen;
        set => SetProperty(ref _isConsultaClienteOpen, value);
    }

    public string FiltroConsultaCliente
    {
        get => _filtroConsultaCliente;
        set
        {
            if (SetProperty(ref _filtroConsultaCliente, value))
            {
                if (IsConsultaClienteOpen)
                {
                    _ = DispararPesquisaAutomaticaClientesAsync();
                }
            }
        }
    }

    public Cliente? ClienteConsultaSelecionado
    {
        get => _clienteConsultaSelecionado;
        set => SetProperty(ref _clienteConsultaSelecionado, value);
    }

    public bool IsBuscandoClientesConsulta
    {
        get => _isBuscandoClientesConsulta;
        set => SetProperty(ref _isBuscandoClientesConsulta, value);
    }

    // --- Propriedades do Modal de Cadastro Rápido ---
    public bool IsCadastroRapidoClienteOpen
    {
        get => _isCadastroRapidoClienteOpen;
        set => SetProperty(ref _isCadastroRapidoClienteOpen, value);
    }

    public string TipoPessoa
    {
        get => _tipoPessoa;
        set
        {
            if (SetProperty(ref _tipoPessoa, value))
            {
                OnPropertyChanged(nameof(IsPessoaJuridica));
                OnPropertyChanged(nameof(IsPessoaFisica));
                OnPropertyChanged(nameof(LabelDocumento));
                OnPropertyChanged(nameof(PlaceholderDocumento));
            }
        }
    }

    public bool IsPessoaJuridica => TipoPessoa == "J";
    public bool IsPessoaFisica => TipoPessoa == "F";
    public string LabelDocumento => IsPessoaJuridica ? "CNPJ" : "CPF";
    public string PlaceholderDocumento => IsPessoaJuridica ? "00.000.000/0000-00" : "000.000.000-00";

    public string NovoClienteNome
    {
        get => _novoClienteNome;
        set => SetProperty(ref _novoClienteNome, value);
    }

    public string NovoClienteFantasia
    {
        get => _novoClienteFantasia;
        set => SetProperty(ref _novoClienteFantasia, value);
    }

    public string NovoClienteDocumento
    {
        get => _novoClienteDocumento;
        set => SetProperty(ref _novoClienteDocumento, value);
    }

    public string NovoClienteTelefone
    {
        get => _novoClienteTelefone;
        set => SetProperty(ref _novoClienteTelefone, value);
    }

    public string NovoClienteCelular
    {
        get => _novoClienteCelular;
        set => SetProperty(ref _novoClienteCelular, value);
    }

    public string NovoClienteCEP
    {
        get => _novoClienteCEP;
        set => SetProperty(ref _novoClienteCEP, value);
    }

    public string NovoClienteEndereco
    {
        get => _novoClienteEndereco;
        set => SetProperty(ref _novoClienteEndereco, value);
    }

    public string NovoClienteNumero
    {
        get => _novoClienteNumero;
        set => SetProperty(ref _novoClienteNumero, value);
    }

    public string NovoClienteBairro
    {
        get => _novoClienteBairro;
        set => SetProperty(ref _novoClienteBairro, value);
    }

    public string NovoClienteCidade
    {
        get => _novoClienteCidade;
        set => SetProperty(ref _novoClienteCidade, value);
    }

    public string NovoClienteUF
    {
        get => _novoClienteUF;
        set => SetProperty(ref _novoClienteUF, value);
    }

    public string NovoClienteEmail
    {
        get => _novoClienteEmail;
        set => SetProperty(ref _novoClienteEmail, value);
    }

    public string CadastroRapidoMensagem
    {
        get => _cadastroRapidoMensagem;
        set => SetProperty(ref _cadastroRapidoMensagem, value);
    }

    public bool IsConsultandoCnpj
    {
        get => _isConsultandoCnpj;
        set => SetProperty(ref _isConsultandoCnpj, value);
    }

    // --- Comandos ---
    public ICommand SalvarCommand { get; }
    public ICommand NovoCommand { get; }
    public ICommand AdicionarItemCommand { get; }
    public ICommand RemoverItemCommand { get; }
    public ICommand BuscarProdutoRapidoCommand { get; }
    public ICommand BuscarClienteRapidoCommand { get; }
    public ICommand LimparClienteCommand { get; }
    public ICommand AlternarTipoCommand { get; }
    public ICommand SelecionarOrcamentoCommand { get; }
    public ICommand SelecionarVendaCommand { get; }
    public ICommand ConverterEmVendaCommand { get; }

    // Comandos de Consulta de Clientes
    public ICommand AbrirConsultaClienteCommand { get; }
    public ICommand FecharConsultaClienteCommand { get; }
    public ICommand PesquisarConsultaClienteCommand { get; }
    public ICommand ConfirmarSelecaoClienteCommand { get; }

    // Comandos de Cadastro Rápido de Cliente
    public ICommand AbrirCadastroRapidoClienteCommand { get; }
    public ICommand FecharCadastroRapidoClienteCommand { get; }
    public ICommand SelecionarPessoaFisicaCommand { get; }
    public ICommand SelecionarPessoaJuridicaCommand { get; }
    public ICommand ConsultarCnpjCommand { get; }
    public ICommand SalvarCadastroRapidoClienteCommand { get; }

    // Notificação / Mensagem de Status
    public ICommand FecharMensagemCommand { get; }

    private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    private System.Threading.CancellationTokenSource? _mensagemCts;

    public void ExibirMensagem(string mensagem, bool isSucesso = true, int duracaoMs = 3500)
    {
        _mensagemCts?.Cancel();
        _mensagemCts = new System.Threading.CancellationTokenSource();
        var token = _mensagemCts.Token;

        StatusMensagem = mensagem;
        IsSucesso = isSucesso;

        if (duracaoMs > 0 && !string.IsNullOrWhiteSpace(mensagem))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(duracaoMs, token);
                    if (!token.IsCancellationRequested)
                    {
                        void Clear()
                        {
                            if (!token.IsCancellationRequested && StatusMensagem == mensagem)
                            {
                                StatusMensagem = string.Empty;
                            }
                        }

                        if (_dispatcherQueue != null)
                        {
                            _dispatcherQueue.TryEnqueue(Clear);
                        }
                        else
                        {
                            Clear();
                        }
                    }
                }
                catch (TaskCanceledException) { }
            });
        }
    }

    public void LimparMensagem()
    {
        _mensagemCts?.Cancel();
        StatusMensagem = string.Empty;
    }

    public PedidoViewModel(
        PedidoService? pedidoService = null,
        ClienteService? clienteService = null,
        ProdutoService? produtoService = null,
        VendedorService? vendedorService = null)
    {
        _pedidoService = pedidoService ?? new PedidoService();
        _clienteService = clienteService ?? new ClienteService();
        _produtoService = produtoService ?? new ProdutoService();
        _vendedorService = vendedorService ?? new VendedorService();

        FecharMensagemCommand = new RelayCommand(_ => LimparMensagem());
        SalvarCommand = new RelayCommand(async _ => await SalvarPedidoAsync());
        NovoCommand = new RelayCommand(_ => NovoPedido());
        AdicionarItemCommand = new RelayCommand(_ => AdicionarItem());
        RemoverItemCommand = new RelayCommand<PedidoItem>(item => RemoverItem(item));
        BuscarProdutoRapidoCommand = new RelayCommand(async _ => await BuscarProdutoRapidoAsync());
        BuscarClienteRapidoCommand = new RelayCommand(async _ => await BuscarClienteRapidoAsync());
        LimparClienteCommand = new RelayCommand(_ => LimparCliente());
        AlternarTipoCommand = new RelayCommand(AlternarTipo);
        SelecionarOrcamentoCommand = new RelayCommand(_ => DefinirTipo("O"));
        SelecionarVendaCommand = new RelayCommand(_ => DefinirTipo("V"));
        ConverterEmVendaCommand = new RelayCommand(async _ => await ConverterEmVendaAsync());

        // Modais de Cliente
        AbrirConsultaClienteCommand = new RelayCommand(async _ => await AbrirConsultaClienteAsync());
        FecharConsultaClienteCommand = new RelayCommand(_ => IsConsultaClienteOpen = false);
        PesquisarConsultaClienteCommand = new RelayCommand(async _ => await PesquisarClientesConsultaAsync());
        ConfirmarSelecaoClienteCommand = new RelayCommand(_ => ConfirmarClienteConsulta());

        AbrirCadastroRapidoClienteCommand = new RelayCommand(_ => AbrirCadastroRapido());
        FecharCadastroRapidoClienteCommand = new RelayCommand(_ => IsCadastroRapidoClienteOpen = false);
        SelecionarPessoaFisicaCommand = new RelayCommand(_ => TipoPessoa = "F");
        SelecionarPessoaJuridicaCommand = new RelayCommand(_ => TipoPessoa = "J");
        ConsultarCnpjCommand = new RelayCommand(async _ => await ConsultarCnpjAsync());
        SalvarCadastroRapidoClienteCommand = new RelayCommand(async _ => await SalvarCadastroRapidoClienteAsync());

        _ = CarregarDadosIniciaisAsync();
    }

    public async Task CarregarDadosIniciaisAsync()
    {
        try
        {
            IsBusy = true;
            Vendedores.Clear();
            var listaVend = await _vendedorService.ListarAsync();
            foreach (var v in listaVend) Vendedores.Add(v);

            if (Vendedores.Count > 0 && SelectedVendedor == null)
            {
                SelectedVendedor = Vendedores[0];
            }
        }
        catch (Exception ex)
        {
            StatusMensagem = $"Erro ao carregar dados auxiliares: {ex.Message}";
            IsSucesso = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void NovoPedido()
    {
        LimparCliente();
        Pedido = new PedidoVenda
        {
            Status = "O",
            Data = DateTime.Today,
            HoraIni = DateTime.Now.ToString("HH:mm"),
            LkUsuario = UsuarioLogado?.Controle,
            LkVendedor = UsuarioLogado?.LkVendedor,
            Vendedor = !string.IsNullOrWhiteSpace(UsuarioLogado?.NomeVendedor) ? UsuarioLogado.NomeVendedor : UsuarioLogado?.Nome
        };
        LimparEntradaItem();
        StatusMensagem = string.Empty;
        NotificarAlteracaoTipo();
    }

    public void DefinirTipo(string tipo)
    {
        Pedido.Status = tipo;
        Pedido.Venda = tipo == "V" ? 1 : 0;
        NotificarAlteracaoTipo();
    }

    public void AlternarTipo(object? _ = null)
    {
        DefinirTipo(IsOrcamento ? "V" : "O");
    }

    private void NotificarAlteracaoTipo()
    {
        OnPropertyChanged(nameof(IsOrcamento));
        OnPropertyChanged(nameof(IsVenda));
        OnPropertyChanged(nameof(TituloTela));
        OnPropertyChanged(nameof(BrushOrcamentoBackground));
        OnPropertyChanged(nameof(BrushOrcamentoForeground));
        OnPropertyChanged(nameof(BrushVendaBackground));
        OnPropertyChanged(nameof(BrushVendaForeground));
        OnTipoDocumentoAlterado?.Invoke();
    }

    public async Task BuscarClienteRapidoAsync()
    {
        if (string.IsNullOrWhiteSpace(FiltroCliente))
        {
            await AbrirConsultaClienteAsync();
            return;
        }

        try
        {
            IsBusy = true;
            var list = await _clienteService.BuscarAsync(FiltroCliente, 25);
            if (list.Count == 1)
            {
                SelecionarCliente(list[0]);
                FiltroCliente = string.Empty;
            }
            else
            {
                FiltroConsultaCliente = FiltroCliente;
                ClientesConsulta.Clear();
                foreach (var c in list) ClientesConsulta.Add(c);
                IsConsultaClienteOpen = true;
            }
        }
        catch (Exception ex)
        {
            StatusMensagem = $"Erro ao buscar cliente: {ex.Message}";
            IsSucesso = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AbrirConsultaClienteAsync()
    {
        FiltroConsultaCliente = FiltroCliente;
        IsConsultaClienteOpen = true;
        await PesquisarClientesConsultaAsync();
        OnConsultaClienteAberta?.Invoke();
    }

    private async Task DispararPesquisaAutomaticaClientesAsync()
    {
        _pesquisaClienteCts?.Cancel();
        _pesquisaClienteCts?.Dispose();
        _pesquisaClienteCts = new CancellationTokenSource();
        var token = _pesquisaClienteCts.Token;

        try
        {
            await Task.Delay(250, token);
            if (token.IsCancellationRequested) return;

            await PesquisarClientesConsultaAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }

    public async Task PesquisarClientesConsultaAsync()
    {
        try
        {
            IsBuscandoClientesConsulta = true;
            var list = await _clienteService.BuscarAsync(FiltroConsultaCliente, 50);

            ClientesConsulta.Clear();
            foreach (var c in list) ClientesConsulta.Add(c);

            if (ClientesConsulta.Count > 0)
            {
                ClienteConsultaSelecionado = ClientesConsulta[0];
            }
            else
            {
                ClienteConsultaSelecionado = null;
            }
        }
        catch (Exception ex)
        {
            StatusMensagem = $"Erro ao listar clientes: {ex.Message}";
        }
        finally
        {
            IsBuscandoClientesConsulta = false;
        }
    }

    public void ConfirmarClienteConsulta()
    {
        var cliente = ClienteConsultaSelecionado ?? ClientesConsulta.FirstOrDefault();
        if (cliente != null)
        {
            SelecionarCliente(cliente);
            IsConsultaClienteOpen = false;
            FiltroCliente = string.Empty;
            OnClienteDefinido?.Invoke();
        }
    }

    public void SelecionarCliente(Cliente cliente)
    {
        SelectedCliente = cliente;
        AtribuirClienteAoPedido(cliente);
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(HasClienteSelecionado));
        ExibirMensagem($"Cliente '{cliente.Nome}' selecionado.", true, 3000);
    }

    private void AtribuirClienteAoPedido(Cliente cliente)
    {
        Pedido.LkCliente = cliente.Controle;
        Pedido.Cliente = cliente.Nome;
        Pedido.CPF_CNPJ = cliente.Documento;
        Pedido.Telefone = !string.IsNullOrWhiteSpace(cliente.Telefone) ? cliente.Telefone : cliente.Celular;
        Pedido.Endereco = cliente.Endereco;
        Pedido.Numero = cliente.Numero;
        Pedido.Bairro = cliente.Bairro;
        Pedido.Cidade = cliente.Cidade;
        Pedido.UF = cliente.Estado;
        Pedido.CEP = cliente.CEP;
        Pedido.Email = cliente.Email;
    }

    public void LimparCliente()
    {
        _selectedCliente = null;
        Pedido.LkCliente = null;
        Pedido.Cliente = string.Empty;
        Pedido.CPF_CNPJ = null;
        Pedido.Telefone = null;
        Pedido.Endereco = null;
        Pedido.Numero = null;
        Pedido.Bairro = null;
        Pedido.Cidade = null;
        Pedido.UF = null;
        Pedido.CEP = null;
        Pedido.Email = null;
        FiltroCliente = string.Empty;

        LimparMensagem();

        OnPropertyChanged(nameof(SelectedCliente));
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(HasClienteSelecionado));
    }

    // --- Métodos de Cadastro Rápido de Cliente ---
    public void AbrirCadastroRapido()
    {
        IsConsultaClienteOpen = false;
        LimparCamposCadastroRapido();

        // Se o que o usuário digitou no campo de busca for um CNPJ ou CPF, já pré-carrega
        string termo = FiltroCliente?.Trim() ?? string.Empty;
        string digitos = System.Text.RegularExpressions.Regex.Replace(termo, @"\D", "");
        if (digitos.Length == 14)
        {
            TipoPessoa = "J";
            NovoClienteDocumento = digitos;
            _ = ConsultarCnpjAsync();
        }
        else if (digitos.Length == 11)
        {
            TipoPessoa = "F";
            NovoClienteDocumento = digitos;
        }
        else if (!string.IsNullOrWhiteSpace(termo))
        {
            NovoClienteNome = termo;
        }

        IsCadastroRapidoClienteOpen = true;
    }

    private void LimparCamposCadastroRapido()
    {
        TipoPessoa = "F";
        NovoClienteNome = string.Empty;
        NovoClienteFantasia = string.Empty;
        NovoClienteDocumento = string.Empty;
        NovoClienteTelefone = string.Empty;
        NovoClienteCelular = string.Empty;
        NovoClienteCEP = string.Empty;
        NovoClienteEndereco = string.Empty;
        NovoClienteNumero = string.Empty;
        NovoClienteBairro = string.Empty;
        NovoClienteCidade = string.Empty;
        NovoClienteUF = string.Empty;
        NovoClienteEmail = string.Empty;
        CadastroRapidoMensagem = string.Empty;
    }

    public async Task ConsultarCnpjAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoClienteDocumento))
        {
            CadastroRapidoMensagem = "Informe o CNPJ para consultar na Receita Federal.";
            return;
        }

        try
        {
            IsConsultandoCnpj = true;
            CadastroRapidoMensagem = "Consultando CNPJ online via BrasilAPI / Receita...";

            var res = await _cnpjLookupService.ConsultarAsync(NovoClienteDocumento);
            if (res.Sucesso)
            {
                if (!string.IsNullOrWhiteSpace(res.RazaoSocial)) NovoClienteNome = res.RazaoSocial;
                if (!string.IsNullOrWhiteSpace(res.NomeFantasia)) NovoClienteFantasia = res.NomeFantasia;
                if (!string.IsNullOrWhiteSpace(res.Cep)) NovoClienteCEP = res.Cep;
                if (!string.IsNullOrWhiteSpace(res.Logradouro)) NovoClienteEndereco = res.Logradouro;
                if (!string.IsNullOrWhiteSpace(res.Numero)) NovoClienteNumero = res.Numero;
                if (!string.IsNullOrWhiteSpace(res.Bairro)) NovoClienteBairro = res.Bairro;
                if (!string.IsNullOrWhiteSpace(res.Municipio)) NovoClienteCidade = res.Municipio;
                if (!string.IsNullOrWhiteSpace(res.Uf)) NovoClienteUF = res.Uf;
                if (!string.IsNullOrWhiteSpace(res.Telefone)) NovoClienteTelefone = res.Telefone;
                if (!string.IsNullOrWhiteSpace(res.Email)) NovoClienteEmail = res.Email;

                CadastroRapidoMensagem = "Dados do CNPJ carregados com sucesso!";
            }
            else
            {
                CadastroRapidoMensagem = res.MensagemErro ?? "Não foi possível localizar dados do CNPJ.";
            }
        }
        catch (Exception ex)
        {
            CadastroRapidoMensagem = $"Erro ao consultar CNPJ: {ex.Message}";
        }
        finally
        {
            IsConsultandoCnpj = false;
        }
    }

    public async Task SalvarCadastroRapidoClienteAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoClienteNome))
        {
            CadastroRapidoMensagem = "O Nome / Razão Social do cliente é obrigatório.";
            return;
        }

        try
        {
            IsBusy = true;
            CadastroRapidoMensagem = "Gravando cliente no banco de dados...";

            var novo = new Cliente
            {
                Nome = NovoClienteNome.Trim(),
                Fantasia = !string.IsNullOrWhiteSpace(NovoClienteFantasia) ? NovoClienteFantasia.Trim() : NovoClienteNome.Trim(),
                Cpf = IsPessoaFisica ? NovoClienteDocumento?.Trim() : null,
                CGC = IsPessoaJuridica ? NovoClienteDocumento?.Trim() : null,
                Telefone = NovoClienteTelefone?.Trim(),
                Celular = NovoClienteCelular?.Trim(),
                Endereco = NovoClienteEndereco?.Trim(),
                Numero = NovoClienteNumero?.Trim(),
                Bairro = NovoClienteBairro?.Trim(),
                Cidade = NovoClienteCidade?.Trim(),
                Estado = NovoClienteUF?.Trim(),
                CEP = NovoClienteCEP?.Trim(),
                Email = NovoClienteEmail?.Trim()
            };

            var gravado = await _clienteService.CadastrarRapidoAsync(novo);

            SelecionarCliente(gravado);
            IsCadastroRapidoClienteOpen = false;
            LimparCamposCadastroRapido();

            StatusMensagem = $"Cliente '{gravado.Nome}' cadastrado (Cód: {gravado.Codigo}) e vinculado ao pedido!";
            IsSucesso = true;
        }
        catch (Exception ex)
        {
            CadastroRapidoMensagem = $"Erro ao cadastrar cliente: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task BuscarProdutoRapidoAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoProdutoInput)) return;

        try
        {
            IsBusy = true;
            var prod = await _produtoService.ObterPorCodigoBarraOuInternoAsync(CodigoProdutoInput);
            if (prod != null)
            {
                ProdutoSelecionado = prod;
            }
            else
            {
                var lista = await _produtoService.BuscarAsync(CodigoProdutoInput, 10);
                ProdutosFiltrados.Clear();
                foreach (var p in lista) ProdutosFiltrados.Add(p);
                if (ProdutosFiltrados.Count > 0)
                {
                    ProdutoSelecionado = ProdutosFiltrados[0];
                }
                else
                {
                    StatusMensagem = "Produto não localizado.";
                    IsSucesso = false;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMensagem = $"Erro ao buscar produto: {ex.Message}";
            IsSucesso = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void AdicionarItem()
    {
        if (ProdutoSelecionado == null)
        {
            StatusMensagem = "Selecione ou bipe um produto antes de adicionar.";
            IsSucesso = false;
            return;
        }

        if (QuantidadeInput <= 0)
        {
            StatusMensagem = "A quantidade deve ser maior que zero.";
            IsSucesso = false;
            return;
        }

        var item = new PedidoItem
        {
            LkProduto = ProdutoSelecionado.Controle,
            CodInterno = ProdutoSelecionado.CodInterno,
            CodBarra = ProdutoSelecionado.CodBarra,
            Produto = ProdutoSelecionado.Descricao,
            Quantidade = QuantidadeInput,
            VlUnitario = ProdutoSelecionado.PrVenda,
            Desconto = DescontoItemInput,
            Unidade = ProdutoSelecionado.Unidade ?? "UN",
            Marca = ProdutoSelecionado.Fabricante,
            LkVendedor = Pedido.LkVendedor ?? UsuarioLogado?.LkVendedor,
            Vendedor = Pedido.Vendedor ?? UsuarioLogado?.NomeVendedor ?? UsuarioLogado?.Nome
        };

        item.RecalcularTotal();
        Pedido.Itens.Add(item);
        Pedido.RecalcularTotais();

        OnPropertyChanged(nameof(Pedido));
        LimparEntradaItem();
        StatusMensagem = $"Item '{item.Produto}' adicionado.";
        IsSucesso = true;
    }

    public void RemoverItem(PedidoItem? item)
    {
        if (item != null && Pedido.Itens.Contains(item))
        {
            Pedido.Itens.Remove(item);
            Pedido.RecalcularTotais();
            OnPropertyChanged(nameof(Pedido));
            StatusMensagem = "Item removido do pedido.";
            IsSucesso = true;
        }
    }

    private void LimparEntradaItem()
    {
        CodigoProdutoInput = string.Empty;
        ProdutoSelecionado = null;
        QuantidadeInput = 1m;
        DescontoItemInput = 0m;
        VlUnitarioInput = 0m;
    }

    public async Task SalvarPedidoAsync()
    {
        if (string.IsNullOrWhiteSpace(Pedido.Cliente))
        {
            StatusMensagem = "Por favor, selecione ou cadastre um cliente para o pedido/orçamento.";
            IsSucesso = false;
            return;
        }

        if (Pedido.Itens.Count == 0)
        {
            StatusMensagem = "Adicione ao menos um item ao pedido antes de salvar.";
            IsSucesso = false;
            return;
        }

        try
        {
            IsBusy = true;
            Pedido.LkUsuario = UsuarioLogado?.Controle;
            Pedido.LkVendedor = UsuarioLogado?.LkVendedor ?? Pedido.LkVendedor;
            Pedido.Vendedor = !string.IsNullOrWhiteSpace(UsuarioLogado?.NomeVendedor) ? UsuarioLogado.NomeVendedor : (Pedido.Vendedor ?? UsuarioLogado?.Nome);

            var salvo = await _pedidoService.SalvarAsync(Pedido);
            Pedido = salvo;
            StatusMensagem = $"{TituloTela} {Pedido.NumeroFormatado} gravado com sucesso no banco de dados!";
            IsSucesso = true;
        }
        catch (Exception ex)
        {
            StatusMensagem = $"Erro ao gravar pedido: {ex.Message}";
            IsSucesso = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ConverterEmVendaAsync()
    {
        if (Pedido.Controle <= 0)
        {
            StatusMensagem = "Salve o orçamento antes de convertê-lo em venda.";
            IsSucesso = false;
            return;
        }

        try
        {
            IsBusy = true;
            bool ok = await _pedidoService.ConverterOrcamentoEmVendaAsync(Pedido.Controle);
            if (ok)
            {
                Pedido.Status = "V";
                Pedido.Venda = 1;
                OnPropertyChanged(nameof(Pedido));
                OnPropertyChanged(nameof(IsOrcamento));
                OnPropertyChanged(nameof(IsVenda));
                StatusMensagem = "Orçamento convertido em Pedido de Venda com sucesso!";
                IsSucesso = true;
            }
        }
        catch (Exception ex)
        {
            StatusMensagem = $"Erro ao converter orçamento: {ex.Message}";
            IsSucesso = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
