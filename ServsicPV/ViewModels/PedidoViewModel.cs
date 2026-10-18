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
    private string _quantidadeTexto = "1";
    private decimal _vlUnitarioInput = 0m;
    private decimal _descontoItemInput = 0m;
    private string _descontoItemTexto = "0";
    private decimal _descontoItemPercentualInput = 0m;
    private string _descontoItemPercentualTexto = "0";
    private string _tipoDescontoItem = "P"; // "P" = %, "V" = R$
    private string _descontoItemInputTexto = "0";
    private bool _isUpdatingDesconto;
    private Produto? _produtoSelecionado;

    // Resumo Financeiro: Modos de Desconto Geral e Acréscimo / Frete
    private string _tipoDescontoGeral = "P";
    private string _descontoGeralInputTexto = "0";
    private string _tipoAcrescimoGeral = "P";
    private string _acrescimoGeralInputTexto = "0";

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
    private bool _isCadastroDuplicadoBloqueado;
    private Cliente? _clienteDuplicadoEncontrado;

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

    public string PrecoUnitarioTexto => ProdutoSelecionado != null ? ProdutoSelecionado.PrVenda.ToString("C2") : "R$ 0,00";

    public string QuantidadeTexto
    {
        get => _quantidadeTexto;
        set
        {
            if (SetProperty(ref _quantidadeTexto, value))
            {
                if (decimal.TryParse(value?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qtd))
                {
                    _quantidadeInput = qtd;
                    OnPropertyChanged(nameof(QuantidadeInput));

                    // Se houver percentual de desconto ativo, recalcula o valor em reais
                    if (_descontoItemPercentualInput > 0 && ProdutoSelecionado != null && !_isUpdatingDesconto)
                    {
                        _isUpdatingDesconto = true;
                        try
                        {
                            decimal subtotal = Math.Round(qtd * ProdutoSelecionado.PrVenda, 2);
                            decimal valorReais = Math.Round((subtotal * _descontoItemPercentualInput) / 100m, 2);
                            _descontoItemInput = valorReais;
                            _descontoItemTexto = valorReais.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
                            if (TipoDescontoItem == "V")
                            {
                                _descontoItemInputTexto = _descontoItemTexto;
                                OnPropertyChanged(nameof(DescontoItemInputTexto));
                            }
                            OnPropertyChanged(nameof(DescontoItemInput));
                            OnPropertyChanged(nameof(DescontoItemTexto));
                        }
                        finally
                        {
                            _isUpdatingDesconto = false;
                        }
                    }
                }
            }
        }
    }

    public decimal QuantidadeInput
    {
        get => _quantidadeInput;
        set
        {
            if (SetProperty(ref _quantidadeInput, value))
            {
                _quantidadeTexto = value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(QuantidadeTexto));
            }
        }
    }

    public decimal VlUnitarioInput
    {
        get => _vlUnitarioInput;
        set => SetProperty(ref _vlUnitarioInput, value);
    }

    public string TipoDescontoItem
    {
        get => _tipoDescontoItem;
        set
        {
            if (SetProperty(ref _tipoDescontoItem, value))
            {
                OnPropertyChanged(nameof(TipoDescontoItemSimbolo));
                OnPropertyChanged(nameof(LabelDescontoItem));
            }
        }
    }

    public string TipoDescontoItemSimbolo => TipoDescontoItem == "P" ? "%" : "R$";
    public string LabelDescontoItem => $"Desc ({TipoDescontoItemSimbolo})";

    public string DescontoItemInputTexto
    {
        get => _descontoItemInputTexto;
        set
        {
            if (SetProperty(ref _descontoItemInputTexto, value))
            {
                if (_isUpdatingDesconto) return;
                _isUpdatingDesconto = true;
                try
                {
                    decimal subtotal = Math.Round(QuantidadeInput * (ProdutoSelecionado?.PrVenda ?? 0m), 2);
                    if (decimal.TryParse(value?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                    {
                        if (TipoDescontoItem == "P")
                        {
                            _descontoItemPercentualInput = num;
                            _descontoItemInput = Math.Round((subtotal * num) / 100m, 2);
                            _descontoItemTexto = _descontoItemInput.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
                            _descontoItemPercentualTexto = num.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            _descontoItemInput = num;
                            _descontoItemPercentualInput = subtotal > 0 ? Math.Round((num / subtotal) * 100m, 2) : 0m;
                            _descontoItemTexto = num.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
                            _descontoItemPercentualTexto = _descontoItemPercentualInput.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                    else
                    {
                        _descontoItemInput = 0m;
                        _descontoItemPercentualInput = 0m;
                        _descontoItemTexto = "0";
                        _descontoItemPercentualTexto = "0";
                    }
                    OnPropertyChanged(nameof(DescontoItemInput));
                    OnPropertyChanged(nameof(DescontoItemTexto));
                    OnPropertyChanged(nameof(DescontoItemPercentualTexto));
                }
                finally
                {
                    _isUpdatingDesconto = false;
                }
            }
        }
    }

    public void AlternarTipoDescontoItem()
    {
        _isUpdatingDesconto = true;
        try
        {
            if (TipoDescontoItem == "P")
            {
                TipoDescontoItem = "V";
                _descontoItemInputTexto = _descontoItemInput > 0 ? _descontoItemInput.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) : "0";
            }
            else
            {
                TipoDescontoItem = "P";
                _descontoItemInputTexto = _descontoItemPercentualInput > 0 ? _descontoItemPercentualInput.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) : "0";
            }
            OnPropertyChanged(nameof(DescontoItemInputTexto));
        }
        finally
        {
            _isUpdatingDesconto = false;
        }
    }

    public string DescontoItemPercentualTexto
    {
        get => _descontoItemPercentualTexto;
        set
        {
            if (SetProperty(ref _descontoItemPercentualTexto, value))
            {
                if (_isUpdatingDesconto) return;
                _isUpdatingDesconto = true;
                try
                {
                    if (decimal.TryParse(value?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var perc))
                    {
                        _descontoItemPercentualInput = perc;
                        decimal subtotal = Math.Round(QuantidadeInput * (ProdutoSelecionado?.PrVenda ?? 0m), 2);
                        decimal valorReais = Math.Round((subtotal * perc) / 100m, 2);
                        _descontoItemInput = valorReais;
                        _descontoItemTexto = valorReais.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
                        if (TipoDescontoItem == "P")
                        {
                            _descontoItemInputTexto = _descontoItemPercentualTexto;
                            OnPropertyChanged(nameof(DescontoItemInputTexto));
                        }
                        OnPropertyChanged(nameof(DescontoItemInput));
                        OnPropertyChanged(nameof(DescontoItemTexto));
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        _descontoItemPercentualInput = 0m;
                        _descontoItemInput = 0m;
                        _descontoItemTexto = "0";
                        if (TipoDescontoItem == "P")
                        {
                            _descontoItemInputTexto = "0";
                            OnPropertyChanged(nameof(DescontoItemInputTexto));
                        }
                        OnPropertyChanged(nameof(DescontoItemInput));
                        OnPropertyChanged(nameof(DescontoItemTexto));
                    }
                }
                finally
                {
                    _isUpdatingDesconto = false;
                }
            }
        }
    }

    public string DescontoItemTexto
    {
        get => _descontoItemTexto;
        set
        {
            if (SetProperty(ref _descontoItemTexto, value))
            {
                if (_isUpdatingDesconto) return;
                _isUpdatingDesconto = true;
                try
                {
                    if (decimal.TryParse(value?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var desc))
                    {
                        _descontoItemInput = desc;
                        decimal subtotal = Math.Round(QuantidadeInput * (ProdutoSelecionado?.PrVenda ?? 0m), 2);
                        decimal perc = subtotal > 0 ? Math.Round((desc / subtotal) * 100m, 2) : 0m;
                        _descontoItemPercentualInput = perc;
                        _descontoItemPercentualTexto = perc.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
                        if (TipoDescontoItem == "V")
                        {
                            _descontoItemInputTexto = _descontoItemTexto;
                            OnPropertyChanged(nameof(DescontoItemInputTexto));
                        }
                        OnPropertyChanged(nameof(DescontoItemInput));
                        OnPropertyChanged(nameof(DescontoItemPercentualTexto));
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        _descontoItemInput = 0m;
                        _descontoItemPercentualInput = 0m;
                        _descontoItemPercentualTexto = "0";
                        if (TipoDescontoItem == "V")
                        {
                            _descontoItemInputTexto = "0";
                            OnPropertyChanged(nameof(DescontoItemInputTexto));
                        }
                        OnPropertyChanged(nameof(DescontoItemInput));
                        OnPropertyChanged(nameof(DescontoItemPercentualTexto));
                    }
                }
                finally
                {
                    _isUpdatingDesconto = false;
                }
            }
        }
    }

    public decimal DescontoItemInput
    {
        get => _descontoItemInput;
        set
        {
            if (SetProperty(ref _descontoItemInput, value))
            {
                _descontoItemTexto = value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(DescontoItemTexto));
            }
        }
    }

    // --- Resumo Financeiro: Desconto Geral e Acréscimo / Frete ---
    public string TipoDescontoGeral
    {
        get => _tipoDescontoGeral;
        set
        {
            if (SetProperty(ref _tipoDescontoGeral, value))
            {
                OnPropertyChanged(nameof(TipoDescontoGeralSimbolo));
            }
        }
    }

    public string TipoDescontoGeralSimbolo => TipoDescontoGeral == "P" ? "%" : "R$";

    public string DescontoGeralInputTexto
    {
        get => _descontoGeralInputTexto;
        set
        {
            if (SetProperty(ref _descontoGeralInputTexto, value))
            {
                if (decimal.TryParse(value?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                {
                    if (TipoDescontoGeral == "P")
                    {
                        Pedido.Desconto = Math.Round((Pedido.Subtotal * num) / 100m, 2);
                    }
                    else
                    {
                        Pedido.Desconto = num;
                    }
                }
                else
                {
                    Pedido.Desconto = 0m;
                }
                Pedido.RecalcularTotais();
                OnPropertyChanged(nameof(Pedido));
            }
        }
    }

    public void AlternarTipoDescontoGeral()
    {
        if (TipoDescontoGeral == "P")
        {
            TipoDescontoGeral = "V";
            _descontoGeralInputTexto = Pedido.Desconto > 0 ? Pedido.Desconto.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) : "0";
        }
        else
        {
            TipoDescontoGeral = "P";
            decimal perc = Pedido.Subtotal > 0 ? Math.Round((Pedido.Desconto / Pedido.Subtotal) * 100m, 2) : 0m;
            _descontoGeralInputTexto = perc > 0 ? perc.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) : "0";
        }
        OnPropertyChanged(nameof(DescontoGeralInputTexto));
    }

    public string TipoAcrescimoGeral
    {
        get => _tipoAcrescimoGeral;
        set
        {
            if (SetProperty(ref _tipoAcrescimoGeral, value))
            {
                OnPropertyChanged(nameof(TipoAcrescimoGeralSimbolo));
            }
        }
    }

    public string TipoAcrescimoGeralSimbolo => TipoAcrescimoGeral == "P" ? "%" : "R$";

    public string AcrescimoGeralInputTexto
    {
        get => _acrescimoGeralInputTexto;
        set
        {
            if (SetProperty(ref _acrescimoGeralInputTexto, value))
            {
                if (decimal.TryParse(value?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                {
                    if (TipoAcrescimoGeral == "P")
                    {
                        Pedido.Acrescimo = Math.Round((Pedido.Subtotal * num) / 100m, 2);
                    }
                    else
                    {
                        Pedido.Acrescimo = num;
                    }
                }
                else
                {
                    Pedido.Acrescimo = 0m;
                }
                Pedido.RecalcularTotais();
                OnPropertyChanged(nameof(Pedido));
            }
        }
    }

    public void AlternarTipoAcrescimoGeral()
    {
        if (TipoAcrescimoGeral == "P")
        {
            TipoAcrescimoGeral = "V";
            _acrescimoGeralInputTexto = Pedido.Acrescimo > 0 ? Pedido.Acrescimo.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) : "0";
        }
        else
        {
            TipoAcrescimoGeral = "P";
            decimal perc = Pedido.Subtotal > 0 ? Math.Round((Pedido.Acrescimo / Pedido.Subtotal) * 100m, 2) : 0m;
            _acrescimoGeralInputTexto = perc > 0 ? perc.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) : "0";
        }
        OnPropertyChanged(nameof(AcrescimoGeralInputTexto));
    }

    public Produto? ProdutoSelecionado
    {
        get => _produtoSelecionado;
        set
        {
            if (SetProperty(ref _produtoSelecionado, value))
            {
                if (value != null)
                {
                    VlUnitarioInput = value.PrVenda;
                    _quantidadeInput = 1m;
                    _quantidadeTexto = "1";
                    _descontoItemInput = 0m;
                    _descontoItemTexto = "0";
                    _descontoItemPercentualInput = 0m;
                    _descontoItemPercentualTexto = "0";
                    _tipoDescontoItem = "P";
                    _descontoItemInputTexto = "0";
                    OnPropertyChanged(nameof(PrecoUnitarioTexto));
                    OnPropertyChanged(nameof(QuantidadeInput));
                    OnPropertyChanged(nameof(QuantidadeTexto));
                    OnPropertyChanged(nameof(DescontoItemInput));
                    OnPropertyChanged(nameof(DescontoItemTexto));
                    OnPropertyChanged(nameof(DescontoItemPercentualTexto));
                    OnPropertyChanged(nameof(TipoDescontoItem));
                    OnPropertyChanged(nameof(TipoDescontoItemSimbolo));
                    OnPropertyChanged(nameof(DescontoItemInputTexto));
                }
                else
                {
                    OnPropertyChanged(nameof(PrecoUnitarioTexto));
                }
                OnPropertyChanged(nameof(HasProdutoSelecionado));
            }
        }
    }

    public bool HasProdutoSelecionado => ProdutoSelecionado != null;
    public System.Collections.ObjectModel.ObservableCollection<PedidoItem> Itens => Pedido.Itens;

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
    public Action? OnProdutoDefinido { get; set; }
    public Action? OnItemAdicionado { get; set; }
    public Action? OnItemAdicaoFalhou { get; set; }
    public Action<string>? OnSolicitarConsultaCliente { get; set; }
    public Action<string>? OnSolicitarConsultaProduto { get; set; }

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

    public bool IsCadastroDuplicadoBloqueado
    {
        get => _isCadastroDuplicadoBloqueado;
        set => SetProperty(ref _isCadastroDuplicadoBloqueado, value);
    }

    public Cliente? ClienteDuplicadoEncontrado
    {
        get => _clienteDuplicadoEncontrado;
        set => SetProperty(ref _clienteDuplicadoEncontrado, value);
    }

    // --- Comandos ---
    public ICommand SalvarCommand { get; }
    public ICommand NovoCommand { get; }
    public ICommand AdicionarItemCommand { get; }
    public ICommand RemoverItemCommand { get; }
    public ICommand BuscarProdutoRapidoCommand { get; }
    public ICommand AbrirConsultaProdutoCommand { get; }
    public ICommand BuscarClienteRapidoCommand { get; }
    public ICommand LimparClienteCommand { get; }
    public ICommand AlternarTipoCommand { get; }
    public ICommand SelecionarOrcamentoCommand { get; }
    public ICommand SelecionarVendaCommand { get; }
    public ICommand ConverterEmVendaCommand { get; }
    public ICommand AlternarTipoDescontoItemCommand { get; }
    public ICommand AlternarTipoDescontoGeralCommand { get; }
    public ICommand AlternarTipoAcrescimoGeralCommand { get; }

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
    public ICommand UsarClienteDuplicadoExistenteCommand { get; }

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
        AbrirConsultaProdutoCommand = new RelayCommand(_ => AbrirConsultaProduto());
        BuscarClienteRapidoCommand = new RelayCommand(async _ => await BuscarClienteRapidoAsync());
        LimparClienteCommand = new RelayCommand(_ => LimparCliente());
        AlternarTipoCommand = new RelayCommand(AlternarTipo);
        SelecionarOrcamentoCommand = new RelayCommand(_ => DefinirTipo("O"));
        SelecionarVendaCommand = new RelayCommand(_ => DefinirTipo("V"));
        ConverterEmVendaCommand = new RelayCommand(async _ => await ConverterEmVendaAsync());
        AlternarTipoDescontoItemCommand = new RelayCommand(_ => AlternarTipoDescontoItem());
        AlternarTipoDescontoGeralCommand = new RelayCommand(_ => AlternarTipoDescontoGeral());
        AlternarTipoAcrescimoGeralCommand = new RelayCommand(_ => AlternarTipoAcrescimoGeral());

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
        UsarClienteDuplicadoExistenteCommand = new RelayCommand(_ => UsarClienteDuplicadoExistente());

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
        _tipoDescontoGeral = "P";
        _descontoGeralInputTexto = "0";
        _tipoAcrescimoGeral = "P";
        _acrescimoGeralInputTexto = "0";
        OnPropertyChanged(nameof(TipoDescontoGeral));
        OnPropertyChanged(nameof(TipoDescontoGeralSimbolo));
        OnPropertyChanged(nameof(DescontoGeralInputTexto));
        OnPropertyChanged(nameof(TipoAcrescimoGeral));
        OnPropertyChanged(nameof(TipoAcrescimoGeralSimbolo));
        OnPropertyChanged(nameof(AcrescimoGeralInputTexto));
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
            AbrirConsultaCliente();
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
                OnClienteDefinido?.Invoke();
            }
            else
            {
                AbrirConsultaCliente(FiltroCliente);
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

    public void AbrirConsultaCliente(string? filtro = null)
    {
        OnSolicitarConsultaCliente?.Invoke(filtro ?? FiltroCliente);
    }

    public async Task AbrirConsultaClienteAsync()
    {
        AbrirConsultaCliente(FiltroCliente);
        await Task.CompletedTask;
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

    public async void SelecionarCliente(Cliente cliente)
    {
        SelectedCliente = cliente;
        AtribuirClienteAoPedido(cliente);
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(SelectedCliente));
        OnPropertyChanged(nameof(HasClienteSelecionado));

        if (cliente.IsBloqueado)
        {
            ExibirMensagem($"Atenção: Cliente '{cliente.Nome}' está {cliente.StatusCredito} para compras a prazo ({cliente.StatusCreditoDescricao}).", false, 6000);
        }
        else
        {
            string infoCred = cliente.LimiteCred > 0 ? $"Limite: {cliente.LimiteFormatado}" : "Sem limite pré-fixado";
            ExibirMensagem($"Cliente '{cliente.Nome}' selecionado ({infoCred} - Liberado a Prazo).", true, 3500);
        }

        try
        {
            var atualizado = await _clienteService.ObterPorIdAsync(cliente.Controle);
            if (atualizado != null && SelectedCliente?.Controle == atualizado.Controle)
            {
                SelectedCliente = atualizado;
                OnPropertyChanged(nameof(SelectedCliente));
            }
        }
        catch
        {
        }
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
        IsCadastroDuplicadoBloqueado = false;
        ClienteDuplicadoEncontrado = null;
    }

    public void UsarClienteDuplicadoExistente()
    {
        if (ClienteDuplicadoEncontrado != null)
        {
            var cli = ClienteDuplicadoEncontrado;
            SelecionarCliente(cli);
            IsCadastroRapidoClienteOpen = false;
            LimparCamposCadastroRapido();
            StatusMensagem = $"Cliente existente '{cli.Nome}' vinculado ao pedido!";
            IsSucesso = true;
            OnClienteDefinido?.Invoke();
        }
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
            CadastroRapidoMensagem = "Verificando dados cadastrais no banco...";
            IsCadastroDuplicadoBloqueado = false;
            ClienteDuplicadoEncontrado = null;

            // 1. Verificação prévia de duplicidade por CPF/CNPJ ou Nome
            string? docLimpo = NovoClienteDocumento?.Trim();
            string? nomeLimpo = NovoClienteNome.Trim();
            var duplicado = await _clienteService.VerificarDuplicidadeAsync(docLimpo, nomeLimpo);
            if (duplicado != null)
            {
                ClienteDuplicadoEncontrado = duplicado;
                IsCadastroDuplicadoBloqueado = true;
                string docInfo = !string.IsNullOrWhiteSpace(duplicado.DocumentoFormatado) ? $" ({duplicado.DocumentoFormatado})" : "";
                CadastroRapidoMensagem = $"Cliente já cadastrado: '{duplicado.Nome}' - Cód: {duplicado.Codigo}{docInfo}.";
                return;
            }

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
            OnClienteDefinido?.Invoke();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Bloqueado: Já existe cliente cadastrado", StringComparison.OrdinalIgnoreCase))
            {
                CadastroRapidoMensagem = ex.Message;
                IsCadastroDuplicadoBloqueado = true;
            }
            else
            {
                CadastroRapidoMensagem = $"Erro ao cadastrar cliente: {ex.Message}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void AbrirConsultaProduto(string? filtro = null)
    {
        OnSolicitarConsultaProduto?.Invoke(filtro ?? CodigoProdutoInput);
    }

    public void SelecionarProdutoDaConsulta(Produto produto)
    {
        ProdutoSelecionado = produto;
        CodigoProdutoInput = !string.IsNullOrWhiteSpace(produto.CodInterno) 
            ? produto.CodInterno 
            : (!string.IsNullOrWhiteSpace(produto.Codigo) ? produto.Codigo : produto.Controle.ToString());
        VlUnitarioInput = produto.PrVenda;
        QuantidadeInput = 1m;
        QuantidadeTexto = "1";
        DescontoItemInput = 0m;
        DescontoItemTexto = "0";
        ExibirMensagem($"Produto '{produto.Descricao}' selecionado ({produto.PrVenda:C2})", true, 3000);
        OnProdutoDefinido?.Invoke();
    }

    public async Task BuscarProdutoRapidoAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoProdutoInput))
        {
            AbrirConsultaProduto();
            return;
        }

        try
        {
            IsBusy = true;
            var prod = await _produtoService.ObterPorCodigoBarraOuInternoAsync(CodigoProdutoInput);
            if (prod != null)
            {
                ProdutoSelecionado = prod;
                OnProdutoDefinido?.Invoke();
            }
            else
            {
                var lista = await _produtoService.BuscarAsync(CodigoProdutoInput, 10);
                if (lista.Count == 1)
                {
                    ProdutoSelecionado = lista[0];
                    OnProdutoDefinido?.Invoke();
                }
                else
                {
                    AbrirConsultaProduto(CodigoProdutoInput);
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
        if (!string.IsNullOrWhiteSpace(QuantidadeTexto) &&
            decimal.TryParse(QuantidadeTexto.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qtdInformada))
        {
            _quantidadeInput = qtdInformada;
            OnPropertyChanged(nameof(QuantidadeInput));
        }

        if (decimal.TryParse(DescontoItemInputTexto?.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var descInformado))
        {
            decimal subtotalCalc = Math.Round(QuantidadeInput * (ProdutoSelecionado?.PrVenda ?? 0m), 2);
            if (TipoDescontoItem == "P")
            {
                _descontoItemPercentualInput = descInformado;
                _descontoItemInput = Math.Round((subtotalCalc * descInformado) / 100m, 2);
            }
            else
            {
                _descontoItemInput = descInformado;
                _descontoItemPercentualInput = subtotalCalc > 0 ? Math.Round((descInformado / subtotalCalc) * 100m, 2) : 0m;
            }
            OnPropertyChanged(nameof(DescontoItemInput));
        }
        else
        {
            _descontoItemInput = 0m;
            _descontoItemPercentualInput = 0m;
            OnPropertyChanged(nameof(DescontoItemInput));
        }

        if (ProdutoSelecionado == null)
        {
            ExibirMensagem("Selecione ou bipe um produto antes de adicionar.", false);
            return;
        }

        if (QuantidadeInput <= 0)
        {
            ExibirMensagem("A quantidade deve ser maior que zero.", false);
            OnItemAdicaoFalhou?.Invoke();
            return;
        }

        if (DescontoItemInput < 0)
        {
            ExibirMensagem("O valor do desconto não pode ser negativo.", false);
            OnItemAdicaoFalhou?.Invoke();
            return;
        }

        decimal subtotalItem = Math.Round(QuantidadeInput * ProdutoSelecionado.PrVenda, 2);
        if (DescontoItemInput > subtotalItem)
        {
            ExibirMensagem($"O desconto (R$ {DescontoItemInput:N2}) não pode ser maior que o subtotal do item (R$ {subtotalItem:N2}).", false, 5000);
            OnItemAdicaoFalhou?.Invoke();
            return;
        }

        // Validação da política de desconto máximo (TabEst1.PerDescMax)
        if (ProdutoSelecionado.PerDescMax > 0 && DescontoItemInput > 0)
        {
            decimal maxDescontoPermitido = Math.Round((subtotalItem * ProdutoSelecionado.PerDescMax) / 100m, 2);
            if (DescontoItemInput > maxDescontoPermitido)
            {
                decimal percInformado = subtotalItem > 0 ? Math.Round((DescontoItemInput / subtotalItem) * 100m, 2) : 0m;
                ExibirMensagem($"Desconto de {percInformado:N2}% (R$ {DescontoItemInput:N2}) ultrapassa o teto máximo permitido de {ProdutoSelecionado.PerDescMax:N2}% (R$ {maxDescontoPermitido:N2}).", false, 6000);
                OnItemAdicaoFalhou?.Invoke();
                return;
            }
        }

        var item = new PedidoItem
        {
            LkProduto = ProdutoSelecionado.Controle,
            CodInterno = !string.IsNullOrWhiteSpace(ProdutoSelecionado.CodInterno) 
                ? ProdutoSelecionado.CodInterno 
                : (!string.IsNullOrWhiteSpace(ProdutoSelecionado.Codigo) ? ProdutoSelecionado.Codigo : ProdutoSelecionado.Controle.ToString()),
            CodBarra = ProdutoSelecionado.CodBarra,
            Produto = ProdutoSelecionado.Descricao,
            Quantidade = QuantidadeInput,
            VlUnitario = ProdutoSelecionado.PrVenda,
            Desconto = DescontoItemInput,
            PerDescMax = ProdutoSelecionado.PerDescMax,
            Unidade = ProdutoSelecionado.Unidade ?? "UN",
            Marca = ProdutoSelecionado.Fabricante,
            LkVendedor = Pedido.LkVendedor ?? UsuarioLogado?.LkVendedor,
            Vendedor = Pedido.Vendedor ?? UsuarioLogado?.NomeVendedor ?? UsuarioLogado?.Nome
        };

        item.RecalcularTotal();
        Pedido.Itens.Add(item);
        Pedido.RecalcularTotais();

        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(Itens));
        LimparEntradaItem();
        ExibirMensagem($"Item '{item.Produto}' adicionado com sucesso.", true, 3000);
        OnItemAdicionado?.Invoke();
    }

    public void RemoverItem(PedidoItem? item)
    {
        if (item != null && Pedido.Itens.Contains(item))
        {
            Pedido.Itens.Remove(item);
            Pedido.RecalcularTotais();
            OnPropertyChanged(nameof(Pedido));
            ExibirMensagem("Item removido do pedido.", true, 2500);
        }
    }

    public bool AtualizarDescontoItem(PedidoItem item, decimal novoDesconto)
    {
        if (item == null) return false;

        if (novoDesconto < 0)
        {
            ExibirMensagem("O valor do desconto não pode ser negativo.", false);
            return false;
        }

        if (novoDesconto > item.SubtotalBruto)
        {
            ExibirMensagem($"O desconto ({novoDesconto:C2}) não pode ser maior que o subtotal do item ({item.SubtotalBruto:C2}).", false, 5000);
            return false;
        }

        if (item.PerDescMax > 0 && novoDesconto > 0)
        {
            decimal maxDesconto = Math.Round((item.SubtotalBruto * item.PerDescMax) / 100m, 2);
            if (novoDesconto > maxDesconto)
            {
                decimal perc = Math.Round((novoDesconto / item.SubtotalBruto) * 100m, 2);
                ExibirMensagem($"Desconto de {perc:N2}% ({novoDesconto:C2}) ultrapassa o teto máximo permitido de {item.PerDescMax:N2}% ({maxDesconto:C2}) para '{item.Produto}'.", false, 6000);
                return false;
            }
        }

        item.Desconto = novoDesconto;
        item.RecalcularTotal();
        Pedido.RecalcularTotais();
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(Itens));
        ExibirMensagem($"Desconto do item '{item.Produto}' atualizado para {novoDesconto:C2}.", true, 3000);
        return true;
    }

    public bool AtualizarDescontoItemPercentual(PedidoItem item, decimal percentual)
    {
        if (item == null) return false;
        decimal valorReais = Math.Round((item.SubtotalBruto * percentual) / 100m, 2);
        return AtualizarDescontoItem(item, valorReais);
    }

    private void LimparEntradaItem()
    {
        CodigoProdutoInput = string.Empty;
        ProdutoSelecionado = null;
        _quantidadeInput = 1m;
        _quantidadeTexto = "1";
        _descontoItemInput = 0m;
        _descontoItemTexto = "0";
        _descontoItemPercentualInput = 0m;
        _descontoItemPercentualTexto = "0";
        _tipoDescontoItem = "P";
        _descontoItemInputTexto = "0";
        _vlUnitarioInput = 0m;
        OnPropertyChanged(nameof(PrecoUnitarioTexto));
        OnPropertyChanged(nameof(QuantidadeInput));
        OnPropertyChanged(nameof(QuantidadeTexto));
        OnPropertyChanged(nameof(DescontoItemInput));
        OnPropertyChanged(nameof(DescontoItemTexto));
        OnPropertyChanged(nameof(DescontoItemPercentualTexto));
        OnPropertyChanged(nameof(TipoDescontoItem));
        OnPropertyChanged(nameof(TipoDescontoItemSimbolo));
        OnPropertyChanged(nameof(DescontoItemInputTexto));
        OnPropertyChanged(nameof(VlUnitarioInput));
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
