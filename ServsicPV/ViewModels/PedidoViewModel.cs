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
    private readonly FormaPagamentoService _formaPagamentoService;
    private readonly CheckoutViewModel _checkoutViewModel;
    private readonly CnpjLookupService _cnpjLookupService = new();
    private readonly ConfiguracaoImpressoraViewModel _configuracaoImpressoraViewModel;
    private readonly CupomVisualizacaoViewModel _cupomVisualizacaoViewModel;
    private readonly ImpressoraConfigService _impressoraConfigService = new();
    private readonly ImpressaoTermicaService _impressaoTermicaService = new();
    private readonly CupomFormatadorService _cupomFormatadorService = new();
    private readonly ConfiguracaoVendaService _configuracaoVendaService = new();
    private readonly AuthService _authService = new();

    // --- Modal: Componente Independente de Checkout ---
    private bool _isCheckoutOpen;
    public CheckoutViewModel CheckoutViewModel => _checkoutViewModel;
    public ConfiguracaoImpressoraViewModel ConfiguracaoImpressoraViewModel => _configuracaoImpressoraViewModel;
    public CupomVisualizacaoViewModel CupomVisualizacaoViewModel => _cupomVisualizacaoViewModel;
    private readonly EnderecoEntregaViewModel _enderecoEntregaViewModel;
    public EnderecoEntregaViewModel EnderecoEntregaViewModel => _enderecoEntregaViewModel;
    private readonly AtualizacaoViewModel _atualizacaoViewModel = new();
    public AtualizacaoViewModel AtualizacaoViewModel => _atualizacaoViewModel;

    private PedidoVenda _pedido = new();
    private Cliente? _selectedCliente;
    private Vendedor? _selectedVendedor;
    private bool _isBusy;
    private string _statusMensagem = string.Empty;
    private bool _isSucesso;
    private string _filtroCliente = string.Empty;
    private readonly HashSet<int> _produtosEstoqueLiberados = new();

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

    // --- Modal: Observações do Pedido ---
    private bool _isObservacoesOpen;

    // --- Modal: Confirmação de Alteração de Tipo (Orçamento / Pré-Venda) ---
    private bool _isConfirmarAlteracaoTipoOpen;
    private string _novoTipoPendente = string.Empty;
    private string _mensagemConfirmacaoTipo = string.Empty;

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
                Pedido.VendedorCelular = value.CelularVendedor;
                Pedido.VendedorTelefone = value.TelefoneVendedor;
                OnPropertyChanged(nameof(Pedido));
                OnPropertyChanged(nameof(LimiteDescontoUsuario));
                RecalcularMargemDescontoItem();
                RecalcularMargemDescontoGeral();
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
            OnPropertyChanged(nameof(EnderecoClienteExibicao));
        }
    }

    public bool HasClienteSelecionado =>
        SelectedCliente != null || (!string.IsNullOrWhiteSpace(Pedido?.Cliente) && (Pedido?.LkCliente ?? 0) > 0);

    public string EnderecoClienteExibicao
    {
        get
        {
            if (Pedido != null && !string.IsNullOrWhiteSpace(Pedido.Endereco))
                return Pedido.EnderecoCompletoFormatado;
            if (SelectedCliente != null && SelectedCliente.HasEndereco)
                return SelectedCliente.EnderecoFormatado;
            return "Endereço não informado";
        }
    }

    public bool HasEnderecoEntregaDefinido => Pedido != null && !string.IsNullOrWhiteSpace(Pedido.Endereco);
    public string ResumoEnderecoEntrega => Pedido?.EnderecoCompletoFormatado ?? string.Empty;

    public bool HasItens => Pedido?.Itens?.Count > 0;

    public bool IsObservacoesOpen
    {
        get => _isObservacoesOpen;
        set => SetProperty(ref _isObservacoesOpen, value);
    }

    public Vendedor? SelectedVendedor
    {
        get => _selectedVendedor;
        set
        {
            if (SetProperty(ref _selectedVendedor, value) && value != null)
            {
                Pedido.LkVendedor = value.Controle;
                Pedido.Vendedor = value.Nome;
                Pedido.VendedorCelular = value.Celular;
                Pedido.VendedorTelefone = value.Telefone;
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
                    RecalcularMargemDescontoItem();
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
                    RecalcularMargemDescontoItem();
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
            RecalcularMargemDescontoItem();
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
                RecalcularMargemDescontoGeral();
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
        RecalcularMargemDescontoGeral();
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
                RecalcularMargemDescontoItem();
            }
        }
    }

    public bool HasProdutoSelecionado => ProdutoSelecionado != null;

    // --- Cálculo de Margem e Política de Desconto (ServNivelLimite + TabEst1.PerDescMax) ---
    public decimal LimiteDescontoUsuario => UsuarioLogado?.LimiteDesconto ?? 0m;

    public decimal LimiteDescontoItemEfetivo
    {
        get
        {
            if (ProdutoSelecionado == null) return LimiteDescontoUsuario;
            if (ProdutoSelecionado.PerDescMax > 0 && LimiteDescontoUsuario > 0)
                return Math.Min(ProdutoSelecionado.PerDescMax, LimiteDescontoUsuario);
            if (ProdutoSelecionado.PerDescMax > 0)
                return ProdutoSelecionado.PerDescMax;
            return LimiteDescontoUsuario;
        }
    }

    public string LimiteDescontoItemInfo
    {
        get
        {
            if (!HasMargemDescontoItemVisivel) return string.Empty;
            string origem = (ProdutoSelecionado?.PerDescMax > 0 && ProdutoSelecionado.PerDescMax < LimiteDescontoUsuario)
                ? "Teto do Produto"
                : $"Nível {UsuarioLogado?.NivelDescricao ?? "Operador"}";
            return $"Teto Máx: {LimiteDescontoItemEfetivo:N2}% ({origem})";
        }
    }

    public decimal DescontoItemPercAtual => _descontoItemPercentualInput;

    public decimal MargemRestanteItemPerc
    {
        get
        {
            if (LimiteDescontoItemEfetivo <= 0) return 0m;
            return LimiteDescontoItemEfetivo - DescontoItemPercAtual;
        }
    }

    public decimal MargemRestanteItemValor
    {
        get
        {
            decimal subtotal = Math.Round(QuantidadeInput * (ProdutoSelecionado?.PrVenda ?? 0m), 2);
            if (subtotal <= 0 || MargemRestanteItemPerc <= 0) return 0m;
            return Math.Round((subtotal * MargemRestanteItemPerc) / 100m, 2);
        }
    }

    public bool HasMargemDescontoItemVisivel => ProdutoSelecionado != null && LimiteDescontoItemEfetivo > 0;

    public string TextoMargemDescontoItemRestante
    {
        get
        {
            if (!HasMargemDescontoItemVisivel) return string.Empty;

            if (MargemRestanteItemPerc > 0)
            {
                return $"Resta {MargemRestanteItemPerc:N2}% ({MargemRestanteItemValor:C2}) a ofertar ao cliente";
            }
            else if (MargemRestanteItemPerc == 0)
            {
                return $"Teto de {LimiteDescontoItemEfetivo:N2}% atingido (0,00% restante)";
            }
            else
            {
                return $"Ultrapassa o teto em +{Math.Abs(MargemRestanteItemPerc):N2}%! Reduza o desconto";
            }
        }
    }

    public Microsoft.UI.Xaml.Media.Brush BrushMargemDescontoItem
    {
        get
        {
            if (MargemRestanteItemPerc < 0)
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38)); // Vermelho
            if (MargemRestanteItemPerc == 0)
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 217, 119, 6)); // Âmbar
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199)); // Azul Corporativo
        }
    }

    public void RecalcularMargemDescontoItem()
    {
        OnPropertyChanged(nameof(LimiteDescontoUsuario));
        OnPropertyChanged(nameof(LimiteDescontoItemEfetivo));
        OnPropertyChanged(nameof(LimiteDescontoItemInfo));
        OnPropertyChanged(nameof(DescontoItemPercAtual));
        OnPropertyChanged(nameof(MargemRestanteItemPerc));
        OnPropertyChanged(nameof(MargemRestanteItemValor));
        OnPropertyChanged(nameof(HasMargemDescontoItemVisivel));
        OnPropertyChanged(nameof(TextoMargemDescontoItemRestante));
        OnPropertyChanged(nameof(BrushMargemDescontoItem));
    }

    // --- Margem de Desconto Geral da Venda / Orçamento ---
    public decimal DescontoGeralPercAtual
    {
        get
        {
            if (Pedido.Subtotal <= 0 || Pedido.Desconto <= 0) return 0m;
            return Math.Round((Pedido.Desconto / Pedido.Subtotal) * 100m, 2);
        }
    }

    public decimal MargemRestanteGeralPerc
    {
        get
        {
            if (LimiteDescontoUsuario <= 0) return 0m;
            return LimiteDescontoUsuario - DescontoGeralPercAtual;
        }
    }

    public decimal MargemRestanteGeralValor
    {
        get
        {
            if (Pedido.Subtotal <= 0 || MargemRestanteGeralPerc <= 0) return 0m;
            return Math.Round((Pedido.Subtotal * MargemRestanteGeralPerc) / 100m, 2);
        }
    }

    public bool HasMargemDescontoGeralVisivel => Pedido.Subtotal > 0 && LimiteDescontoUsuario > 0;

    public string TextoMargemDescontoGeralRestante
    {
        get
        {
            if (!HasMargemDescontoGeralVisivel) return string.Empty;

            if (MargemRestanteGeralPerc > 0)
            {
                return $"Resta {MargemRestanteGeralPerc:N2}% ({MargemRestanteGeralValor:C2}) a ofertar | Teto: {LimiteDescontoUsuario:N2}%";
            }
            else if (MargemRestanteGeralPerc == 0)
            {
                return $"Teto de {LimiteDescontoUsuario:N2}% atingido (0,00% restante)";
            }
            else
            {
                return $"Ultrapassa o teto em +{Math.Abs(MargemRestanteGeralPerc):N2}%! Limite: {LimiteDescontoUsuario:N2}%";
            }
        }
    }

    public Microsoft.UI.Xaml.Media.Brush BrushMargemDescontoGeral
    {
        get
        {
            if (MargemRestanteGeralPerc < 0)
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38));
            if (MargemRestanteGeralPerc == 0)
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 217, 119, 6));
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199));
        }
    }

    public void RecalcularMargemDescontoGeral()
    {
        OnPropertyChanged(nameof(DescontoGeralPercAtual));
        OnPropertyChanged(nameof(MargemRestanteGeralPerc));
        OnPropertyChanged(nameof(MargemRestanteGeralValor));
        OnPropertyChanged(nameof(HasMargemDescontoGeralVisivel));
        OnPropertyChanged(nameof(TextoMargemDescontoGeralRestante));
        OnPropertyChanged(nameof(BrushMargemDescontoGeral));
    }

    public System.Collections.ObjectModel.ObservableCollection<PedidoItem> Itens => Pedido.Itens;

    public bool IsOrcamento => string.Equals(Pedido?.Status, "O", StringComparison.OrdinalIgnoreCase)
        || (!string.Equals(Pedido?.Status, "P", StringComparison.OrdinalIgnoreCase) && !string.Equals(Pedido?.Status, "V", StringComparison.OrdinalIgnoreCase));
    public bool IsVenda => string.Equals(Pedido?.Status, "P", StringComparison.OrdinalIgnoreCase) || string.Equals(Pedido?.Status, "V", StringComparison.OrdinalIgnoreCase);
    public string TituloTela => "Pedido de Venda";

    public bool LancarPagamentos
    {
        get => true;
        set { }
    }

    public string TextoBotaoSalvar => "Totalizar (F6)";

    public string StatusPagamentoModoTexto => "Totalizar e finalizar pedido";

    public Microsoft.UI.Xaml.Media.Brush BrushOrcamentoBackground => IsOrcamento
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public Microsoft.UI.Xaml.Media.Brush BrushOrcamentoBorder => IsOrcamento
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public Microsoft.UI.Xaml.Media.Brush BrushOrcamentoForeground => IsOrcamento
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139));

    public Microsoft.UI.Xaml.Media.Brush BrushVendaBackground => IsVenda
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 25, 44))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public Microsoft.UI.Xaml.Media.Brush BrushVendaBorder => IsVenda
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 25, 44))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public Microsoft.UI.Xaml.Media.Brush BrushVendaForeground => IsVenda
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139));

    public string TipoDocumentoDescricao => IsOrcamento ? "ORÇAMENTO" : "PRÉ-VENDA";

    public Microsoft.UI.Xaml.Media.Brush BrushTipoBadgeBackground => IsOrcamento
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 25, 44));

    public Action? OnTipoDocumentoAlterado { get; set; }
    public Action? OnConsultaClienteAberta { get; set; }
    public Action? OnClienteDefinido { get; set; }
    public Action? OnProdutoDefinido { get; set; }
    public Action? OnItemAdicionado { get; set; }
    public Action? OnItemAdicaoFalhou { get; set; }
    public Action? OnCheckoutAberto { get; set; }
    public Action<string>? OnSolicitarConsultaCliente { get; set; }
    public Action<string>? OnSolicitarConsultaProduto { get; set; }

    // --- Propriedades do Modal de Checkout (Condições de Pagamento) ---
    public bool IsCheckoutOpen
    {
        get => _checkoutViewModel?.IsOpen ?? _isCheckoutOpen;
        set
        {
            if (_checkoutViewModel != null && _checkoutViewModel.IsOpen != value)
            {
                _checkoutViewModel.IsOpen = value;
            }
            SetProperty(ref _isCheckoutOpen, value);
        }
    }

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

    // --- Propriedades de Confirmação de Alteração de Tipo ---
    public bool IsConfirmarAlteracaoTipoOpen
    {
        get => _isConfirmarAlteracaoTipoOpen;
        set => SetProperty(ref _isConfirmarAlteracaoTipoOpen, value);
    }

    public string NovoTipoPendente
    {
        get => _novoTipoPendente;
        set => SetProperty(ref _novoTipoPendente, value);
    }

    public string MensagemConfirmacaoTipo
    {
        get => _mensagemConfirmacaoTipo;
        set => SetProperty(ref _mensagemConfirmacaoTipo, value);
    }

    // --- Proteção de Dados Não Salvos (Gravação Tardia) ---
    public enum TipoAcaoPendenteAposSalvar
    {
        Nenhuma,
        NovoPedido,
        CarregarOutroPedido,
        Sair
    }

    private bool _isConfirmarSalvarPendenteOpen;
    private string _mensagemConfirmacaoSalvarPendente = string.Empty;
    public TipoAcaoPendenteAposSalvar AcaoPendente { get; private set; } = TipoAcaoPendenteAposSalvar.Nenhuma;
    private Action? _callbackAcaoPendente;

    public bool IsConfirmarSalvarPendenteOpen
    {
        get => _isConfirmarSalvarPendenteOpen;
        set => SetProperty(ref _isConfirmarSalvarPendenteOpen, value);
    }

    public string MensagemConfirmacaoSalvarPendente
    {
        get => _mensagemConfirmacaoSalvarPendente;
        set => SetProperty(ref _mensagemConfirmacaoSalvarPendente, value);
    }

    /// <summary>
    /// Indica se o pedido/venda carregado está concluído, fechado pelo caixa ou cancelado, bloqueando terminantemente qualquer edição.
    /// </summary>
    public bool IsEdicaoBloqueada => Pedido?.IsConcluidaOuFechada == true;

    /// <summary>
    /// Indica se a venda/orçamento em tela pode ser editado pelo operador.
    /// </summary>
    public bool IsEdicaoPermitida => !IsEdicaoBloqueada;

    public string BannerBloqueioTitulo => $"VENDA #{Pedido?.NumeroFormatado} CONCLUÍDA • SOMENTE LEITURA";
    public string BannerBloqueioMensagem => "Esta venda já foi concretizada no caixa. Nenhuma informação ou item pode ser alterado pelo sistema.";

    public bool TemDadosNaoSalvos => (Pedido?.Itens?.Count > 0) && !IsEdicaoBloqueada;

    // --- Liberação de Gerente (Descontos Excedentes e Trava de Estoque) ---
    public enum TipoLiberacaoGerente
    {
        Nenhum,
        DescontoItem,
        DescontoItemEdicao,
        DescontoGeral,
        EstoqueInsuficienteItem,
        EstoqueInsuficienteTotalizacao
    }

    private bool _isLiberacaoGerenteOpen;
    private string _tituloLiberacaoGerente = "Liberação de Gerente";
    private string _subtituloLiberacaoGerente = "Autorização de Exceção Operacional";
    private string _motivoLiberacaoGerente = string.Empty;
    private string _detalheLiberacaoGerente = string.Empty;
    private string _senhaGerenteInput = string.Empty;
    private string _mensagemErroLiberacao = string.Empty;
    private Usuario? _gerenteSelecionado;
    private Func<Usuario, Task>? _callbackAcaoLiberada;
    public ObservableCollection<Usuario> GerentesDisponiveis { get; } = new();

    public bool IsLiberacaoGerenteOpen
    {
        get => _isLiberacaoGerenteOpen;
        set => SetProperty(ref _isLiberacaoGerenteOpen, value);
    }

    public string TituloLiberacaoGerente
    {
        get => _tituloLiberacaoGerente;
        set => SetProperty(ref _tituloLiberacaoGerente, value);
    }

    public string SubtituloLiberacaoGerente
    {
        get => _subtituloLiberacaoGerente;
        set => SetProperty(ref _subtituloLiberacaoGerente, value);
    }

    public string MotivoLiberacaoGerente
    {
        get => _motivoLiberacaoGerente;
        set => SetProperty(ref _motivoLiberacaoGerente, value);
    }

    public string DetalheLiberacaoGerente
    {
        get => _detalheLiberacaoGerente;
        set => SetProperty(ref _detalheLiberacaoGerente, value);
    }

    public string SenhaGerenteInput
    {
        get => _senhaGerenteInput;
        set => SetProperty(ref _senhaGerenteInput, value);
    }

    public string MensagemErroLiberacao
    {
        get => _mensagemErroLiberacao;
        set => SetProperty(ref _mensagemErroLiberacao, value);
    }

    public Usuario? GerenteSelecionado
    {
        get => _gerenteSelecionado;
        set => SetProperty(ref _gerenteSelecionado, value);
    }

    public Action? OnSolicitarFocoSenhaGerente;

    // --- Comandos ---
    public ICommand SalvarCommand { get; }
    public ICommand ReimprimirComprovanteCommand { get; }
    public ICommand SalvarPendenteCommand { get; }
    public ICommand ConfirmarSalvarPendenteCommand { get; }
    public ICommand DescartarEContinuarCommand { get; }
    public ICommand CancelarSalvarPendenteCommand { get; }
    public ICommand ConfirmarLiberacaoGerenteCommand { get; }
    public ICommand CancelarLiberacaoGerenteCommand { get; }
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
    public ICommand ConfirmarAlteracaoTipoCommand { get; }
    public ICommand CancelarAlteracaoTipoCommand { get; }
    public ICommand AlternarTipoDescontoItemCommand { get; }
    public ICommand AlternarTipoDescontoGeralCommand { get; }
    public ICommand AlternarTipoAcrescimoGeralCommand { get; }

    // Comandos de Entrega e Multi-Endereço
    public ICommand AbrirEnderecoEntregaCommand { get; }
    public ICommand RemoverEnderecoEntregaCommand { get; }
    public ICommand AlternarEntregaItemCommand { get; }
    public ICommand MarcarTodosItensEntregaCommand { get; }

    // Comandos de Checkout (Condições de Pagamento)
    public ICommand AbrirCheckoutCommand { get; }
    public ICommand FecharCheckoutCommand { get; }

    // Comandos de Consulta de Clientes
    public ICommand AbrirConsultaClienteCommand { get; }
    public ICommand FecharConsultaClienteCommand { get; }
    public ICommand PesquisarConsultaClienteCommand { get; }
    public ICommand ConfirmarSelecaoClienteCommand { get; }

    // Comandos de Observações do Pedido
    public ICommand AbrirObservacoesCommand { get; }
    public ICommand FecharObservacoesCommand { get; }

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
        VendedorService? vendedorService = null,
        FormaPagamentoService? formaPagamentoService = null)
    {
        _pedidoService = pedidoService ?? new PedidoService();
        _clienteService = clienteService ?? new ClienteService();
        _produtoService = produtoService ?? new ProdutoService();
        _vendedorService = vendedorService ?? new VendedorService();
        _formaPagamentoService = formaPagamentoService ?? new FormaPagamentoService();

        _configuracaoImpressoraViewModel = new ConfiguracaoImpressoraViewModel(_impressoraConfigService, _impressaoTermicaService);
        _cupomVisualizacaoViewModel = new CupomVisualizacaoViewModel(_impressoraConfigService, _impressaoTermicaService, _cupomFormatadorService);

        _enderecoEntregaViewModel = new EnderecoEntregaViewModel();
        _enderecoEntregaViewModel.OnEnderecoConfirmado += () =>
        {
            bool temEntrega = HasEnderecoEntregaDefinido;
            Pedido.Entrega = temEntrega ? 1 : 0;
            if (temEntrega)
            {
                if (!Pedido.DataEntrega.HasValue) Pedido.DataEntrega = DateTime.Today;
                foreach (var it in Pedido.Itens)
                {
                    it.Entrega = 1;
                    it.IsEntrega = true;
                }
            }
            else
            {
                foreach (var it in Pedido.Itens)
                {
                    it.Entrega = 0;
                    it.IsEntrega = false;
                }
            }

            OnPropertyChanged(nameof(Pedido));
            OnPropertyChanged(nameof(EnderecoClienteExibicao));
            OnPropertyChanged(nameof(HasEnderecoEntregaDefinido));
            OnPropertyChanged(nameof(ResumoEnderecoEntrega));
        };

        _checkoutViewModel = new CheckoutViewModel(_formaPagamentoService);
        _checkoutViewModel.PagamentoConfirmado += async (pagamentos) =>
        {
            try
            {
                IsBusy = true;
                Pedido.Pagamentos.Clear();
                foreach (var p in pagamentos)
                {
                    Pedido.Pagamentos.Add(p);
                }

                // Status no balcão: 'O' para Orçamento, 'P' para Pedido de Venda / Pré-Venda
                Pedido.Status = Pedido.IsOrcamento ? "O" : "P";
                Pedido.Venda = 0; // Balcão: não finaliza fiscalmente

                decimal troco = _checkoutViewModel.ValorTroco;
                await ExecutarGravacaoPedidoAsync();

                _checkoutViewModel.IsOpen = false;
                IsCheckoutOpen = false;

                var pedidoFinalizado = Pedido; // Mantém referência para impressão
                ProcessarImpressaoEResetar(pedidoFinalizado, troco);
            }
            catch (Exception ex)
            {
                _checkoutViewModel.Mensagem = $"Erro ao gravar pedido no banco: {ex.Message}";
                _checkoutViewModel.IsOpen = true;
                IsCheckoutOpen = true;
            }
            finally
            {
                IsBusy = false;
            }
        };

        _checkoutViewModel.CheckoutCancelado += () =>
        {
            IsCheckoutOpen = false;
        };

        FecharMensagemCommand = new RelayCommand(_ => LimparMensagem());
        SalvarCommand = new RelayCommand(async _ => await SalvarPedidoAsync());
        ReimprimirComprovanteCommand = new RelayCommand(async _ => await ReimprimirComprovanteAsync());
        AbrirCheckoutCommand = new RelayCommand(async _ => await AbrirCheckoutAsync());
        FecharCheckoutCommand = new RelayCommand(_ => FecharCheckout());

        NovoCommand = new RelayCommand(_ => SolicitarNovoPedido());
        SalvarPendenteCommand = new RelayCommand(async _ => await SalvarPedidoPendenteDiretoAsync());
        ConfirmarSalvarPendenteCommand = new RelayCommand(async _ => await ConfirmarSalvarPendenteAsync());
        DescartarEContinuarCommand = new RelayCommand(_ => DescartarEContinuar());
        CancelarSalvarPendenteCommand = new RelayCommand(_ => CancelarSalvarPendente());
        ConfirmarLiberacaoGerenteCommand = new RelayCommand(async _ => await ConfirmarLiberacaoGerenteAsync());
        CancelarLiberacaoGerenteCommand = new RelayCommand(_ => CancelarLiberacaoGerente());
        AdicionarItemCommand = new RelayCommand(_ => AdicionarItem());
        RemoverItemCommand = new RelayCommand<PedidoItem>(item => RemoverItem(item));
        BuscarProdutoRapidoCommand = new RelayCommand(async _ => await BuscarProdutoRapidoAsync());
        AbrirConsultaProdutoCommand = new RelayCommand(_ => AbrirConsultaProduto());
        BuscarClienteRapidoCommand = new RelayCommand(async _ => await BuscarClienteRapidoAsync());
        LimparClienteCommand = new RelayCommand(_ => LimparCliente());
        AlternarTipoCommand = new RelayCommand(async _ => await SelecionarTipoAsync(IsOrcamento ? "P" : "O"));
        SelecionarOrcamentoCommand = new RelayCommand(async _ => await SelecionarTipoAsync("O"));
        SelecionarVendaCommand = new RelayCommand(async _ => await SelecionarTipoAsync("P"));
        ConfirmarAlteracaoTipoCommand = new RelayCommand(_ => ConfirmarAlteracaoTipo());
        CancelarAlteracaoTipoCommand = new RelayCommand(_ => CancelarAlteracaoTipo());
        AlternarTipoDescontoItemCommand = new RelayCommand(_ => AlternarTipoDescontoItem());
        AlternarTipoDescontoGeralCommand = new RelayCommand(_ => AlternarTipoDescontoGeral());
        AlternarTipoAcrescimoGeralCommand = new RelayCommand(_ => AlternarTipoAcrescimoGeral());

        AbrirEnderecoEntregaCommand = new RelayCommand(async _ => await AbrirEnderecoEntregaAsync());
        RemoverEnderecoEntregaCommand = new RelayCommand(_ => RemoverEnderecoEntrega());
        AlternarEntregaItemCommand = new RelayCommand<PedidoItem>(item =>
        {
            if (item != null)
            {
                item.IsEntrega = !item.IsEntrega;
                OnPropertyChanged(nameof(Pedido));
            }
        });
        MarcarTodosItensEntregaCommand = new RelayCommand(_ =>
        {
            if (Pedido?.Itens == null || Pedido.Itens.Count == 0) return;
            bool todosMarcados = Pedido.Itens.All(i => i.IsEntrega);
            foreach (var i in Pedido.Itens)
            {
                i.IsEntrega = !todosMarcados;
            }
            OnPropertyChanged(nameof(Pedido));
        });

        // Modais de Cliente
        AbrirConsultaClienteCommand = new RelayCommand(async _ => await AbrirConsultaClienteAsync());
        FecharConsultaClienteCommand = new RelayCommand(_ => IsConsultaClienteOpen = false);
        PesquisarConsultaClienteCommand = new RelayCommand(async _ => await PesquisarClientesConsultaAsync());
        ConfirmarSelecaoClienteCommand = new RelayCommand(_ => ConfirmarClienteConsulta());

        AbrirObservacoesCommand = new RelayCommand(_ => IsObservacoesOpen = true);
        FecharObservacoesCommand = new RelayCommand(_ => IsObservacoesOpen = false);

        AbrirCadastroRapidoClienteCommand = new RelayCommand(_ => AbrirCadastroRapido());
        FecharCadastroRapidoClienteCommand = new RelayCommand(_ => IsCadastroRapidoClienteOpen = false);
        SelecionarPessoaFisicaCommand = new RelayCommand(_ => TipoPessoa = "F");
        SelecionarPessoaJuridicaCommand = new RelayCommand(_ => TipoPessoa = "J");
        ConsultarCnpjCommand = new RelayCommand(async _ => await ConsultarCnpjAsync());
        SalvarCadastroRapidoClienteCommand = new RelayCommand(async _ => await SalvarCadastroRapidoClienteAsync());
        UsarClienteDuplicadoExistenteCommand = new RelayCommand(_ => UsarClienteDuplicadoExistente());

        NotificarAlteracaoTipo();
        _ = CarregarDadosIniciaisAsync();
    }

    public async Task CarregarDadosIniciaisAsync()
    {
        try
        {
            IsBusy = true;
            _ = _configuracaoVendaService.ObterConfiguracaoAsync();
            Vendedores.Clear();
            var listaVend = await _vendedorService.ListarAsync();
            foreach (var v in listaVend) Vendedores.Add(v);

            if (UsuarioLogado?.LkVendedor.HasValue == true)
            {
                SelectedVendedor = Vendedores.FirstOrDefault(v => v.Controle == UsuarioLogado.LkVendedor.Value);
            }
        }
        catch (Exception ex)
        {
            ExibirMensagem($"Erro ao carregar dados auxiliares: {ex.Message}", false, 5000);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SolicitarNovoPedido()
    {
        if (!TemDadosNaoSalvos)
        {
            NovoPedido();
            return;
        }

        AcaoPendente = TipoAcaoPendenteAposSalvar.NovoPedido;
        _callbackAcaoPendente = () => NovoPedido(preservarMensagem: true);
        string tipoNome = IsOrcamento ? "Orçamento" : "Pré-Venda";
        MensagemConfirmacaoSalvarPendente = $"Existe um {tipoNome} aberto com {Pedido.Itens.Count} item(ns) no valor total de {Pedido.Total:C2}.\n\nDeseja salvá-lo como PENDENTE para não perder o atendimento antes de iniciar um novo pedido?";
        IsConfirmarSalvarPendenteOpen = true;
    }

    public void SolicitarCarregarPedido(int controle, Action<int> executarCarga)
    {
        if (!TemDadosNaoSalvos)
        {
            executarCarga(controle);
            return;
        }

        AcaoPendente = TipoAcaoPendenteAposSalvar.CarregarOutroPedido;
        _callbackAcaoPendente = () => executarCarga(controle);
        string tipoNome = IsOrcamento ? "Orçamento" : "Pré-Venda";
        MensagemConfirmacaoSalvarPendente = $"Existe um {tipoNome} em andamento com {Pedido.Itens.Count} item(ns) no valor de {Pedido.Total:C2}.\n\nDeseja salvá-lo como PENDENTE antes de carregar o outro pedido?";
        IsConfirmarSalvarPendenteOpen = true;
    }

    public void SolicitarSair(Action executarSaida)
    {
        if (!TemDadosNaoSalvos)
        {
            executarSaida();
            return;
        }

        AcaoPendente = TipoAcaoPendenteAposSalvar.Sair;
        _callbackAcaoPendente = executarSaida;
        string tipoNome = IsOrcamento ? "Orçamento" : "Pré-Venda";
        MensagemConfirmacaoSalvarPendente = $"Existe um {tipoNome} aberto com {Pedido.Itens.Count} item(ns) no valor de {Pedido.Total:C2}.\n\nDeseja salvá-lo como PENDENTE antes de sair do sistema?";
        IsConfirmarSalvarPendenteOpen = true;
    }

    public async Task<bool> SalvarPedidoPendenteAsync()
    {
        if (!TemDadosNaoSalvos) return false;
        if (IsBusy) return false;

        try
        {
            IsBusy = true;
            return await SalvarPedidoPendenteInternoAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> SalvarPedidoPendenteInternoAsync()
    {
        try
        {
            Pedido.Status = IsOrcamento ? "O" : "P";
            Pedido.Venda = 0;
            Pedido.Cancelada = 0;

            await ExecutarGravacaoPedidoAsync();

            string tipoNome = IsOrcamento ? "Orçamento" : "Pré-Venda";
            ExibirMensagem($"{tipoNome} #{Pedido.Pedido} salvo com sucesso como PENDENTE!", true, 5000);
            return true;
        }
        catch (Exception ex)
        {
            ExibirMensagem($"Erro ao salvar {(IsOrcamento ? "orçamento" : "pré-venda")} como pendente: {ex.Message}", false, 7000);
            return false;
        }
    }

    public async Task SalvarPedidoPendenteDiretoAsync()
    {
        if (!TemDadosNaoSalvos)
        {
            ExibirMensagem("Não há itens no pedido para salvar.", false, 4000);
            return;
        }

        bool sucesso = await SalvarPedidoPendenteAsync();
        if (sucesso)
        {
            NovoPedido(preservarMensagem: true);
        }
    }

    public async Task ConfirmarSalvarPendenteAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        IsConfirmarSalvarPendenteOpen = false;

        var callback = _callbackAcaoPendente;
        AcaoPendente = TipoAcaoPendenteAposSalvar.Nenhuma;
        _callbackAcaoPendente = null;

        try
        {
            bool sucesso = await SalvarPedidoPendenteInternoAsync();
            if (sucesso)
            {
                callback?.Invoke();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void DescartarEContinuar()
    {
        if (IsBusy) return;
        IsConfirmarSalvarPendenteOpen = false;
        var callback = _callbackAcaoPendente;
        AcaoPendente = TipoAcaoPendenteAposSalvar.Nenhuma;
        _callbackAcaoPendente = null;

        if (callback != null)
        {
            callback.Invoke();
        }
        else
        {
            NovoPedido();
        }
    }

    public void CancelarSalvarPendente()
    {
        IsConfirmarSalvarPendenteOpen = false;
        AcaoPendente = TipoAcaoPendenteAposSalvar.Nenhuma;
        _callbackAcaoPendente = null;
    }

    // --- Métodos de Liberação de Gerente ---
    public async Task AbrirLiberacaoGerenteAsync(string titulo, string subtitulo, string motivo, string detalhe, Func<Usuario, Task> acaoAposLiberacao)
    {
        TituloLiberacaoGerente = titulo;
        SubtituloLiberacaoGerente = subtitulo;
        MotivoLiberacaoGerente = motivo;
        DetalheLiberacaoGerente = detalhe;
        SenhaGerenteInput = string.Empty;
        MensagemErroLiberacao = string.Empty;
        _callbackAcaoLiberada = acaoAposLiberacao;

        try
        {
            if (GerentesDisponiveis.Count == 0)
            {
                var lista = await _authService.ListarGerentesAtivosAsync();
                GerentesDisponiveis.Clear();
                foreach (var g in lista)
                {
                    GerentesDisponiveis.Add(g);
                }
            }

            if (UsuarioLogado != null && UsuarioLogado.Nivel >= 1)
            {
                var usuarioGerente = GerentesDisponiveis.FirstOrDefault(g => g.Controle == UsuarioLogado.Controle);
                GerenteSelecionado = usuarioGerente ?? (GerentesDisponiveis.Count > 0 ? GerentesDisponiveis[0] : null);
            }
            else if (GerentesDisponiveis.Count > 0)
            {
                GerenteSelecionado = GerentesDisponiveis[0];
            }
        }
        catch (Exception ex)
        {
            MensagemErroLiberacao = $"Erro ao carregar gerentes: {ex.Message}";
        }

        IsLiberacaoGerenteOpen = true;
        OnSolicitarFocoSenhaGerente?.Invoke();
    }

    public async Task ConfirmarLiberacaoGerenteAsync()
    {
        if (GerenteSelecionado == null)
        {
            MensagemErroLiberacao = "Selecione o gerente responsável.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SenhaGerenteInput))
        {
            MensagemErroLiberacao = "Digite a senha do gerente.";
            OnSolicitarFocoSenhaGerente?.Invoke();
            return;
        }

        try
        {
            IsBusy = true;
            MensagemErroLiberacao = "Validando autorização...";

            var (sucesso, mensagem, gerenteAutenticado) = await _authService.ValidarLiberacaoGerenteAsync(GerenteSelecionado.Controle, SenhaGerenteInput);
            if (!sucesso || gerenteAutenticado == null)
            {
                MensagemErroLiberacao = mensagem;
                SenhaGerenteInput = string.Empty;
                OnSolicitarFocoSenhaGerente?.Invoke();
                return;
            }

            // Liberação confirmada!
            IsLiberacaoGerenteOpen = false;
            MensagemErroLiberacao = string.Empty;
            SenhaGerenteInput = string.Empty;

            var acao = _callbackAcaoLiberada;
            _callbackAcaoLiberada = null;

            if (acao != null)
            {
                await acao.Invoke(gerenteAutenticado);
            }

            ExibirMensagem($"Operação liberada com sucesso por {gerenteAutenticado.Nome}.", true, 3500);
        }
        catch (Exception ex)
        {
            MensagemErroLiberacao = $"Erro ao validar liberação: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void CancelarLiberacaoGerente()
    {
        IsLiberacaoGerenteOpen = false;
        _callbackAcaoLiberada = null;
        SenhaGerenteInput = string.Empty;
        MensagemErroLiberacao = string.Empty;
    }

    public void NovoPedido(bool preservarMensagem = false)
    {
        _produtosEstoqueLiberados.Clear();
        _selectedCliente = null;
        FiltroCliente = string.Empty;

        var novoPedido = new PedidoVenda
        {
            Status = "O",
            Data = DateTime.Today,
            HoraIni = DateTime.Now.ToString("HH:mm"),
            LkUsuario = UsuarioLogado?.Controle,
            LkVendedor = UsuarioLogado?.LkVendedor,
            Vendedor = !string.IsNullOrWhiteSpace(UsuarioLogado?.NomeVendedor) ? UsuarioLogado.NomeVendedor : UsuarioLogado?.Nome,
            VendedorCelular = UsuarioLogado?.CelularVendedor,
            VendedorTelefone = UsuarioLogado?.TelefoneVendedor,
            IdEmpresa = 2
        };
        Pedido = novoPedido;

        _enderecoEntregaViewModel.LimparCampos();

        _tipoDescontoGeral = "P";
        _descontoGeralInputTexto = "0";
        _tipoAcrescimoGeral = "P";
        _acrescimoGeralInputTexto = "0";

        LimparEntradaItem();

        if (!preservarMensagem)
        {
            StatusMensagem = string.Empty;
        }

        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(Itens));
        OnPropertyChanged(nameof(HasItens));
        OnPropertyChanged(nameof(TemDadosNaoSalvos));
        OnPropertyChanged(nameof(SelectedCliente));
        OnPropertyChanged(nameof(HasClienteSelecionado));
        OnPropertyChanged(nameof(EnderecoClienteExibicao));
        OnPropertyChanged(nameof(HasEnderecoEntregaDefinido));
        OnPropertyChanged(nameof(ResumoEnderecoEntrega));
        OnPropertyChanged(nameof(FiltroCliente));
        OnPropertyChanged(nameof(TipoDescontoGeral));
        OnPropertyChanged(nameof(TipoDescontoGeralSimbolo));
        OnPropertyChanged(nameof(DescontoGeralInputTexto));
        OnPropertyChanged(nameof(TipoAcrescimoGeral));
        OnPropertyChanged(nameof(TipoAcrescimoGeralSimbolo));
        OnPropertyChanged(nameof(AcrescimoGeralInputTexto));
        NotificarAlteracaoTipo();
        RecalcularMargemDescontoGeral();
        NotificarEstadoBloqueio();
        OnClienteDefinido?.Invoke();
    }

    public async Task CarregarPedidoAsync(PedidoVenda pedidoCarregado)
    {
        Pedido = pedidoCarregado;
        _produtosEstoqueLiberados.Clear();
        foreach (var item in Pedido.Itens)
        {
            item.EstoqueLiberado = true;
            _produtosEstoqueLiberados.Add(item.LkProduto);
        }
        FiltroCliente = pedidoCarregado.Cliente;
        _descontoGeralInputTexto = pedidoCarregado.Desconto.ToString("N2");
        _acrescimoGeralInputTexto = pedidoCarregado.Acrescimo.ToString("N2");

        if (pedidoCarregado.LkCliente.HasValue && pedidoCarregado.LkCliente.Value > 0)
        {
            try
            {
                var cli = await _clienteService.ObterPorIdAsync(pedidoCarregado.LkCliente.Value);
                if (cli != null)
                {
                    SelectedCliente = cli;
                }
                else
                {
                    SelectedCliente = new Cliente
                    {
                        Controle = pedidoCarregado.LkCliente.Value,
                        Nome = pedidoCarregado.Cliente ?? string.Empty,
                        Telefone = pedidoCarregado.Telefone,
                        Endereco = pedidoCarregado.Endereco,
                        Numero = pedidoCarregado.Numero,
                        ComplEnd = pedidoCarregado.ComplEnd,
                        Bairro = pedidoCarregado.Bairro,
                        Cidade = pedidoCarregado.Cidade,
                        Estado = pedidoCarregado.UF,
                        CEP = pedidoCarregado.CEP
                    };
                }
            }
            catch
            {
                SelectedCliente = new Cliente
                {
                    Controle = pedidoCarregado.LkCliente.Value,
                    Nome = pedidoCarregado.Cliente ?? string.Empty,
                    Telefone = pedidoCarregado.Telefone,
                    Endereco = pedidoCarregado.Endereco,
                    Numero = pedidoCarregado.Numero,
                    ComplEnd = pedidoCarregado.ComplEnd,
                    Bairro = pedidoCarregado.Bairro,
                    Cidade = pedidoCarregado.Cidade,
                    Estado = pedidoCarregado.UF,
                    CEP = pedidoCarregado.CEP
                };
            }
        }
        else
        {
            SelectedCliente = null;
        }

        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(Itens));
        OnPropertyChanged(nameof(HasItens));
        OnPropertyChanged(nameof(HasClienteSelecionado));
        OnPropertyChanged(nameof(SelectedCliente));
        OnPropertyChanged(nameof(EnderecoClienteExibicao));
        OnPropertyChanged(nameof(TipoDescontoGeral));
        OnPropertyChanged(nameof(TipoDescontoGeralSimbolo));
        OnPropertyChanged(nameof(DescontoGeralInputTexto));
        OnPropertyChanged(nameof(TipoAcrescimoGeral));
        OnPropertyChanged(nameof(TipoAcrescimoGeralSimbolo));
        OnPropertyChanged(nameof(AcrescimoGeralInputTexto));
        NotificarAlteracaoTipo();
        NotificarEstadoBloqueio();

        if (pedidoCarregado.IsConcluidaOuFechada)
        {
            ExibirMensagem($"🔒 Venda #{pedidoCarregado.NumeroFormatado} carregada em MODO SOMENTE LEITURA. Nenhuma alteração é permitida.", false, 6000);
        }
        else
        {
            ExibirMensagem($"{(pedidoCarregado.Status == "O" ? "Orçamento" : "Pedido")} #{pedidoCarregado.Pedido} carregado com sucesso.", true, 3500);
        }
    }

    public void DefinirTipo(string tipo)
    {
        if (IsEdicaoBloqueada) return;

        if (string.IsNullOrWhiteSpace(tipo)) tipo = "O";
        tipo = tipo.Trim().ToUpperInvariant();
        if (tipo != "O" && tipo != "P") tipo = "O";

        Pedido.Status = tipo;
        Pedido.Venda = 0;
        NotificarAlteracaoTipo();
    }

    public async Task SelecionarTipoAsync(string tipo)
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem("Venda concluída não permite alternar entre Orçamento e Pré-Venda.", false, 4000);
            return;
        }

        if (string.IsNullOrWhiteSpace(tipo)) tipo = "O";
        tipo = tipo.Trim().ToUpperInvariant();
        if (tipo != "O" && tipo != "P" && tipo != "V") tipo = "O";

        if (string.Equals(Pedido.Status, tipo, StringComparison.OrdinalIgnoreCase)) return;

        bool novoIsOrcamento = tipo == "O";
        bool permite = await _configuracaoVendaService.PermiteEstoqueInsuficienteAsync(novoIsOrcamento);
        if (!permite && Pedido.Itens.Count > 0)
        {
            var itensPorProduto = Pedido.Itens.GroupBy(i => i.LkProduto);
            foreach (var grp in itensPorProduto)
            {
                decimal qtdTotal = grp.Sum(i => i.Quantidade);
                decimal estoqueAtual = await _produtoService.ObterEstoqueAtualAsync(grp.Key);
                if (qtdTotal > estoqueAtual)
                {
                    string nomeDestino = novoIsOrcamento ? "Orçamento" : "Pré-Venda";
                    ExibirMensagem($"Não é possível alterar para {nomeDestino}: o item '{grp.First().Produto}' possui quantidade ({qtdTotal:N2}) maior que o estoque ({estoqueAtual:N2}). Operação com estoque insuficiente bloqueada pelo ERP.", false, 7000);
                    return;
                }
            }
        }

        DefinirTipo(tipo);
    }

    public void AlternarTipo(object? _ = null)
    {
        _ = SelecionarTipoAsync(IsOrcamento ? "P" : "O");
    }

    public void SolicitarAlteracaoTipo(string tipo)
    {
        _ = SelecionarTipoAsync(tipo);
    }

    public void ConfirmarAlteracaoTipo()
    {
        if (!string.IsNullOrEmpty(NovoTipoPendente))
        {
            _ = SelecionarTipoAsync(NovoTipoPendente);
        }
        IsConfirmarAlteracaoTipoOpen = false;
        NovoTipoPendente = string.Empty;
    }

    public void CancelarAlteracaoTipo()
    {
        IsConfirmarAlteracaoTipoOpen = false;
        NovoTipoPendente = string.Empty;
    }

    private void NotificarAlteracaoTipo()
    {
        OnPropertyChanged(nameof(IsOrcamento));
        OnPropertyChanged(nameof(IsVenda));
        OnPropertyChanged(nameof(TituloTela));
        OnPropertyChanged(nameof(TipoDocumentoDescricao));
        OnPropertyChanged(nameof(BrushTipoBadgeBackground));
        OnPropertyChanged(nameof(BrushOrcamentoBackground));
        OnPropertyChanged(nameof(BrushOrcamentoBorder));
        OnPropertyChanged(nameof(BrushOrcamentoForeground));
        OnPropertyChanged(nameof(BrushVendaBackground));
        OnPropertyChanged(nameof(BrushVendaBorder));
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
            ExibirMensagem($"Erro ao buscar cliente: {ex.Message}", false, 5000);
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

    public async Task AbrirEnderecoEntregaAsync()
    {
        await _enderecoEntregaViewModel.AbrirAsync(Pedido, SelectedCliente);
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
            ExibirMensagem($"Erro ao listar clientes: {ex.Message}", false, 5000);
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
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem($"Venda #{Pedido.NumeroFormatado} concluída não permite alterar cliente.", false, 4000);
            return;
        }

        SelectedCliente = cliente;
        AtribuirClienteAoPedido(cliente);
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(SelectedCliente));
        OnPropertyChanged(nameof(HasClienteSelecionado));
        OnPropertyChanged(nameof(EnderecoClienteExibicao));

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
                OnPropertyChanged(nameof(EnderecoClienteExibicao));
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
        Pedido.ComplEnd = cliente.ComplEnd;
        Pedido.Bairro = cliente.Bairro;
        Pedido.Cidade = cliente.Cidade;
        Pedido.UF = cliente.Estado;
        Pedido.CEP = cliente.CEP;
        Pedido.Email = cliente.Email;
        Pedido.SaldoLimite = cliente.LimiteCred;
    }

    public void LimparCliente(bool preservarMensagem = false)
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem($"Venda #{Pedido.NumeroFormatado} concluída não permite desvincular o cliente.", false, 4000);
            return;
        }

        _selectedCliente = null;
        Pedido.LkCliente = null;
        Pedido.Cliente = string.Empty;
        Pedido.CPF_CNPJ = null;
        Pedido.Telefone = null;
        Pedido.Endereco = null;
        Pedido.Numero = null;
        Pedido.ComplEnd = null;
        Pedido.Bairro = null;
        Pedido.Cidade = null;
        Pedido.UF = null;
        Pedido.CEP = null;
        Pedido.Email = null;
        Pedido.Obs = null;
        Pedido.LkEnderecoCli = null;
        Pedido.Entrega = 0;
        FiltroCliente = string.Empty;

        _enderecoEntregaViewModel.LimparCampos();

        if (!preservarMensagem)
        {
            LimparMensagem();
        }

        OnPropertyChanged(nameof(SelectedCliente));
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(HasClienteSelecionado));
        OnPropertyChanged(nameof(EnderecoClienteExibicao));
        OnPropertyChanged(nameof(HasEnderecoEntregaDefinido));
        OnPropertyChanged(nameof(ResumoEnderecoEntrega));
    }

    public void RemoverEnderecoEntrega()
    {
        if (IsEdicaoBloqueada) return;
        if (Pedido != null)
        {
            Pedido.Endereco = null;
            Pedido.Numero = null;
            Pedido.ComplEnd = null;
            Pedido.Bairro = null;
            Pedido.Cidade = null;
            Pedido.UF = null;
            Pedido.CEP = null;
            Pedido.Obs = null;
            Pedido.LkEnderecoCli = null;
            Pedido.Entrega = 0;
            foreach (var it in Pedido.Itens)
            {
                it.Entrega = 0;
                it.IsEntrega = false;
            }
        }
        _enderecoEntregaViewModel.LimparCampos();
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(HasEnderecoEntregaDefinido));
        OnPropertyChanged(nameof(ResumoEnderecoEntrega));
        OnPropertyChanged(nameof(EnderecoClienteExibicao));
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
            ExibirMensagem($"Cliente existente '{cli.Nome}' vinculado ao pedido!", true, 3500);
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

            ExibirMensagem($"Cliente '{gravado.Nome}' cadastrado (Cód: {gravado.Codigo}) e vinculado ao pedido!", true, 3500);
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
            ExibirMensagem($"Erro ao buscar produto: {ex.Message}", false, 5000);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void AdicionarItem()
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem($"Venda #{Pedido.NumeroFormatado} concluída não permite inclusão de itens.", false, 4000);
            return;
        }

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
            ExibirMensagem($"O desconto ({DescontoItemInput:C2}) não pode ser maior que o subtotal do item ({subtotalItem:C2}).", false, 5000);
            OnItemAdicaoFalhou?.Invoke();
            return;
        }

        // Validação da política de desconto máximo (TabEst1.PerDescMax e ServNivelLimite do Usuário)
        bool precisaLiberacaoDesconto = false;
        decimal percInformado = subtotalItem > 0 ? Math.Round((DescontoItemInput / subtotalItem) * 100m, 2) : 0m;
        decimal maxDescontoPermitido = (LimiteDescontoItemEfetivo > 0) ? Math.Round((subtotalItem * LimiteDescontoItemEfetivo) / 100m, 2) : subtotalItem;

        if (LimiteDescontoItemEfetivo > 0 && DescontoItemInput > maxDescontoPermitido)
        {
            if (UsuarioLogado?.Nivel >= 2)
            {
                if (UsuarioLogado.LimiteDesconto > 0 && percInformado > UsuarioLogado.LimiteDesconto)
                {
                    ExibirMensagem($"Desconto de {percInformado:N2}% ultrapassa seu próprio teto de gerente ({UsuarioLogado.LimiteDesconto:N2}%).", false, 6000);
                    OnItemAdicaoFalhou?.Invoke();
                    return;
                }
            }
            else
            {
                precisaLiberacaoDesconto = true;
            }
        }

        // Validação da política de estoque insuficiente do ERP Legado (CFG_ORCAMENTO / CFG_PREVENDA)
        bool permiteEstoqueInsuficiente = _configuracaoVendaService.PermiteEstoqueInsuficiente(Pedido.IsOrcamento);
        decimal qtdJaNoPedido = Pedido.Itens.Where(i => i.LkProduto == ProdutoSelecionado.Controle).Sum(i => i.Quantidade);
        decimal qtdTotalDesejada = qtdJaNoPedido + QuantidadeInput;
        bool precisaLiberacaoEstoque = false;

        if (!permiteEstoqueInsuficiente && qtdTotalDesejada > ProdutoSelecionado.Estoque)
        {
            if (UsuarioLogado?.Nivel < 2 && !_produtosEstoqueLiberados.Contains(ProdutoSelecionado.Controle))
            {
                precisaLiberacaoEstoque = true;
            }
            else
            {
                _produtosEstoqueLiberados.Add(ProdutoSelecionado.Controle);
            }
        }

        if (precisaLiberacaoDesconto || precisaLiberacaoEstoque)
        {
            var prod = ProdutoSelecionado;
            decimal qtd = QuantidadeInput;
            decimal desc = DescontoItemInput;

            string tipoOperacao = Pedido.IsOrcamento ? "Orçamento" : "Pré-Venda";
            string titulo = (precisaLiberacaoDesconto && precisaLiberacaoEstoque)
                ? "Liberação: Desconto e Estoque"
                : (precisaLiberacaoDesconto ? "Liberação: Desconto Excedente" : "Liberação: Estoque Insuficiente");
            string subtitulo = $"{tipoOperacao} • Autorização de Gerente";

            var motivos = new System.Collections.Generic.List<string>();
            if (precisaLiberacaoDesconto)
            {
                motivos.Add($"Desconto de {percInformado:N2}% ({desc:C2}) ultrapassa o limite permitido ({LimiteDescontoItemEfetivo:N2}% / {maxDescontoPermitido:C2}).");
            }
            if (precisaLiberacaoEstoque)
            {
                string statusEst = prod.Estoque <= 0 ? "Sem Estoque (0,00)" : $"{prod.Estoque:N2} {prod.Unidade}";
                motivos.Add($"Estoque insuficiente ({statusEst} | Solicitado: {qtdTotalDesejada:N2} {prod.Unidade}).");
            }

            string motivo = string.Join("\n", motivos);
            string detalhe = $"Produto: {prod.Descricao}\nQuantidade: {qtd:N2} {prod.Unidade} | Subtotal: {subtotalItem:C2}\nInforme a senha de um Gerente ou Supervisor para autorizar a operação.";

            _ = AbrirLiberacaoGerenteAsync(titulo, subtitulo, motivo, detalhe, async (gerente) =>
            {
                string obs = $"liberado por {gerente.Nome}";
                if (precisaLiberacaoEstoque)
                {
                    _produtosEstoqueLiberados.Add(prod.Controle);
                }
                InserirItemInterno(qtd, desc, obs, estoqueLiberado: precisaLiberacaoEstoque);
                await Task.CompletedTask;
            });
            return;
        }

        InserirItemInterno(QuantidadeInput, DescontoItemInput);
    }

    private void InserirItemInterno(decimal qtd, decimal desconto, string? observacaoLiberacao = null, bool estoqueLiberado = false)
    {
        if (ProdutoSelecionado == null) return;

        bool isLiberado = estoqueLiberado || _produtosEstoqueLiberados.Contains(ProdutoSelecionado.Controle) || (UsuarioLogado?.Nivel >= 2);

        // Se o produto já foi inserido anteriormente no pedido, acumula quantidade em vez de duplicar linha
        var itemExistente = Pedido.Itens.FirstOrDefault(i => i.LkProduto == ProdutoSelecionado.Controle);
        if (itemExistente != null)
        {
            itemExistente.Quantidade += qtd;
            itemExistente.Desconto += desconto;
            if (isLiberado)
            {
                itemExistente.EstoqueLiberado = true;
                _produtosEstoqueLiberados.Add(itemExistente.LkProduto);
            }
            itemExistente.RecalcularTotal();
            Pedido.RecalcularTotais();

            OnPropertyChanged(nameof(Pedido));
            OnPropertyChanged(nameof(Itens));
            OnPropertyChanged(nameof(HasItens));
            RecalcularMargemDescontoGeral();
            LimparEntradaItem();

            string msg = !string.IsNullOrWhiteSpace(observacaoLiberacao)
                ? $"Quantidade de '{itemExistente.Produto}' acumulada para {itemExistente.Quantidade:N2} {itemExistente.Unidade} ({observacaoLiberacao})."
                : $"Quantidade de '{itemExistente.Produto}' acumulada para {itemExistente.Quantidade:N2} {itemExistente.Unidade}.";

            ExibirMensagem(msg, true, 3500);
            OnItemAdicionado?.Invoke();
            return;
        }

        var item = new PedidoItem
        {
            LkProduto = ProdutoSelecionado.Controle,
            CodInterno = !string.IsNullOrWhiteSpace(ProdutoSelecionado.CodInterno) 
                ? ProdutoSelecionado.CodInterno 
                : (!string.IsNullOrWhiteSpace(ProdutoSelecionado.Codigo) ? ProdutoSelecionado.Codigo : ProdutoSelecionado.Controle.ToString()),
            CodBarra = ProdutoSelecionado.CodBarra,
            CodOrigem = ProdutoSelecionado.CodOrigem,
            Produto = ProdutoSelecionado.Descricao,
            Quantidade = qtd,
            VlUnitario = ProdutoSelecionado.PrVenda,
            PrecoVenda = ProdutoSelecionado.PrVenda,
            PrCusto = ProdutoSelecionado.PrCusto,
            Desconto = desconto,
            PerDescMax = ProdutoSelecionado.PerDescMax,
            EstoqueLiberado = isLiberado,
            Unidade = ProdutoSelecionado.Unidade ?? "UN",
            Marca = ProdutoSelecionado.Fabricante,
            LkVendedor = UsuarioLogado?.LkVendedor ?? Pedido.LkVendedor,
            Vendedor = !string.IsNullOrWhiteSpace(UsuarioLogado?.NomeVendedor) 
                ? UsuarioLogado.NomeVendedor 
                : (Pedido.Vendedor ?? UsuarioLogado?.Nome),
            Cfop = ProdutoSelecionado.Cfop > 0 ? ProdutoSelecionado.Cfop : 5405,
            Cst = int.TryParse(ProdutoSelecionado.Cst, out int parsedProdCst) 
                ? parsedProdCst.ToString() 
                : ((ProdutoSelecionado.Cst == "FF" || (ProdutoSelecionado.Cfop > 0 ? ProdutoSelecionado.Cfop : 5405) == 5405) ? "60" : "102"),
            CstEcf = (ProdutoSelecionado.Cst == "60" || ProdutoSelecionado.Cst == "FF" || (ProdutoSelecionado.Cfop > 0 ? ProdutoSelecionado.Cfop : 5405) == 5405) ? "FF" : "01",
            QtdSaldo = ProdutoSelecionado.EstoqueLoja > 0 ? ProdutoSelecionado.EstoqueLoja : ProdutoSelecionado.Estoque,
            Peso = ProdutoSelecionado.Peso ?? 0,
            IdEmpresa = Pedido.IdEmpresa,
            Entrega = HasEnderecoEntregaDefinido ? 1 : 0,
            IsEntrega = HasEnderecoEntregaDefinido
        };
        if (isLiberado)
        {
            _produtosEstoqueLiberados.Add(item.LkProduto);
        }

        item.RecalcularTotal();
        Pedido.Itens.Add(item);
        Pedido.RecalcularTotais();

        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(Itens));
        OnPropertyChanged(nameof(HasItens));
        RecalcularMargemDescontoGeral();
        LimparEntradaItem();
        
        string msgNova = !string.IsNullOrWhiteSpace(observacaoLiberacao)
            ? $"Item '{item.Produto}' adicionado ({observacaoLiberacao})."
            : $"Item '{item.Produto}' adicionado com sucesso.";
            
        ExibirMensagem(msgNova, true, 3500);
        OnItemAdicionado?.Invoke();
    }

    public void RemoverItem(PedidoItem? item)
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem($"Venda #{Pedido.NumeroFormatado} concluída não permite exclusão de itens.", false, 4000);
            return;
        }

        if (item != null && Pedido.Itens.Contains(item))
        {
            Pedido.Itens.Remove(item);
            Pedido.RecalcularTotais();
            OnPropertyChanged(nameof(Pedido));
            OnPropertyChanged(nameof(HasItens));
            RecalcularMargemDescontoGeral();
            ExibirMensagem("Item removido do pedido.", true, 2500);
        }
    }

    public bool AtualizarQuantidadeItem(PedidoItem item, decimal novaQuantidade)
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem($"Venda #{Pedido.NumeroFormatado} concluída não permite alteração de quantidade.", false, 4000);
            return false;
        }

        if (item == null) return false;

        if (novaQuantidade <= 0)
        {
            ExibirMensagem("A quantidade deve ser maior que zero.", false, 4000);
            return false;
        }

        item.Quantidade = novaQuantidade;

        // Se o desconto em reais ficou superior ao subtotal bruto com a nova quantidade, ajusta
        if (item.Desconto > item.SubtotalBruto)
        {
            item.Desconto = item.SubtotalBruto;
        }

        item.RecalcularTotal();
        Pedido.RecalcularTotais();

        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(Itens));
        OnPropertyChanged(nameof(HasItens));
        RecalcularMargemDescontoGeral();

        ExibirMensagem($"Quantidade de '{item.Produto}' atualizada para {novaQuantidade:N2} {item.Unidade}.", true, 3000);
        return true;
    }

    public bool AtualizarDescontoItem(PedidoItem item, decimal novoDesconto)
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem($"Venda #{Pedido.NumeroFormatado} concluída não permite alteração de desconto.", false, 4000);
            return false;
        }

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

        decimal limiteEfetivo = item.PerDescMax > 0 && LimiteDescontoUsuario > 0 
            ? Math.Min(item.PerDescMax, LimiteDescontoUsuario) 
            : (item.PerDescMax > 0 ? item.PerDescMax : LimiteDescontoUsuario);

        if (limiteEfetivo > 0 && novoDesconto > 0)
        {
            decimal maxDesconto = Math.Round((item.SubtotalBruto * limiteEfetivo) / 100m, 2);
            if (novoDesconto > maxDesconto)
            {
                decimal perc = Math.Round((novoDesconto / item.SubtotalBruto) * 100m, 2);
                if (UsuarioLogado?.Nivel >= 2)
                {
                    if (UsuarioLogado.LimiteDesconto > 0 && perc > UsuarioLogado.LimiteDesconto)
                    {
                        ExibirMensagem($"Desconto de {perc:N2}% ultrapassa seu próprio teto de gerente ({UsuarioLogado.LimiteDesconto:N2}%).", false, 6000);
                        return false;
                    }
                }
                else
                {
                    string titulo = "Liberação: Desconto do Item";
                    string subtitulo = "Alteração de Item • Autorização de Gerente";
                    string motivo = $"Desconto alterado para {perc:N2}% ({novoDesconto:C2}) ultrapassa o limite do vendedor ({limiteEfetivo:N2}% / {maxDesconto:C2}).";
                    string detalhe = $"Item: {item.Produto}\nSubtotal: {item.SubtotalBruto:C2} | Novo Desconto: {novoDesconto:C2} ({perc:N2}%)\nInforme a senha de um Gerente ou Supervisor para autorizar.";

                    _ = AbrirLiberacaoGerenteAsync(titulo, subtitulo, motivo, detalhe, async (gerente) =>
                    {
                        item.Desconto = novoDesconto;
                        item.RecalcularTotal();
                        Pedido.RecalcularTotais();
                        OnPropertyChanged(nameof(Pedido));
                        OnPropertyChanged(nameof(Itens));
                        RecalcularMargemDescontoGeral();
                        ExibirMensagem($"Desconto do item '{item.Produto}' atualizado para {novoDesconto:C2} (liberado por {gerente.Nome}).", true, 3500);
                        await Task.CompletedTask;
                    });
                    return false;
                }
            }
        }

        item.Desconto = novoDesconto;
        item.RecalcularTotal();
        Pedido.RecalcularTotais();
        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(Itens));
        RecalcularMargemDescontoGeral();
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

    // ==============================================================
    // IMPRESSÃO E CONCLUSÃO DE FLUXO DE PEDIDO / ORÇAMENTO
    // ==============================================================

    private void ProcessarImpressaoEResetar(PedidoVenda pedidoFinalizado, decimal troco = 0)
    {
        string tipoDescricao = pedidoFinalizado.IsOrcamento ? "Orçamento" : "Pedido de Venda";
        string msgExtra = troco > 0 ? $" DEVOLVER TROCO: {troco:C2}." : "";

        // Prepara a tela para um novo pedido preservando a mensagem de sucesso
        NovoPedido(preservarMensagem: true);

        // 🖨️ Fluxo de Impressão de Pedido de Venda / Orçamento
        var configImpressora = _impressoraConfigService.ObterConfiguracao();
        if (configImpressora.ModoImpressao == "Direto" && !string.IsNullOrWhiteSpace(configImpressora.NomeImpressora))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var empresa = await _impressoraConfigService.ObterDadosEmpresaAsync();
                    string textoCupom = _cupomFormatadorService.GerarTextoCupom(pedidoFinalizado, empresa, configImpressora);
                    int vias = configImpressora.DuasVias ? 2 : 1;
                    for (int i = 0; i < vias; i++)
                    {
                        _impressaoTermicaService.ImprimirTexto(configImpressora.NomeImpressora, textoCupom, configImpressora.CortarPapel);
                    }

                    // 🚚 Impressão Automática de Itens para Entrega conforme configuração do ERP (CFG_VENDA)
                    var configVenda = await _configuracaoVendaService.ObterConfiguracaoAsync();
                    if (configVenda.ImprimeItensEntregaAutomaticamente && pedidoFinalizado.TemItensEntrega)
                    {
                        string textoEntrega = _cupomFormatadorService.GerarTextoCupomEntrega(pedidoFinalizado, empresa, configImpressora);
                        _impressaoTermicaService.ImprimirTexto(configImpressora.NomeImpressora, textoEntrega, configImpressora.CortarPapel);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Impressao] Erro: {ex.Message}");
                }
            });

            string acaoNome = troco > 0 || (pedidoFinalizado.Pagamentos != null && pedidoFinalizado.Pagamentos.Count > 0) ? "totalizado" : "finalizado";
            ExibirMensagem($"✓ {tipoDescricao} Nº {pedidoFinalizado.NumeroFormatado} {acaoNome} e enviado para a impressora!{msgExtra}", true, 6000);
        }
        else if (configImpressora.ModoImpressao == "Previa")
        {
            ExibirMensagem($"✓ {tipoDescricao} Nº {pedidoFinalizado.NumeroFormatado} concluído com sucesso!{msgExtra}", true, 6000);

            // Abre o modal de pré-visualização do documento
            _ = _cupomVisualizacaoViewModel.CarregarAsync(pedidoFinalizado);
        }
        else
        {
            // Modo NaoImprimir
            string acaoNome = troco > 0 || (pedidoFinalizado.Pagamentos != null && pedidoFinalizado.Pagamentos.Count > 0) ? "totalizado" : "finalizado";
            ExibirMensagem($"✓ {tipoDescricao} Nº {pedidoFinalizado.NumeroFormatado} {acaoNome} e gravado com sucesso!{msgExtra}", true, 6000);
        }
    }

    /// <summary>
    /// Reimpressão direta do comprovante de venda sem realizar qualquer alteração nos dados do banco.
    /// </summary>
    public async Task ReimprimirComprovanteAsync()
    {
        if (Pedido == null) return;
        try
        {
            var config = _impressoraConfigService.ObterConfiguracao();
            if (string.IsNullOrWhiteSpace(config.NomeImpressora))
            {
                ExibirMensagem("Nenhuma impressora configurada. Acesse as configurações no topo para definir a impressora.", false, 5000);
                return;
            }

            var empresa = await _impressoraConfigService.ObterDadosEmpresaAsync();
            string textoCupom = _cupomFormatadorService.GerarTextoCupom(Pedido, empresa, config);
            int vias = config.DuasVias ? 2 : 1;
            for (int i = 0; i < vias; i++)
            {
                _impressaoTermicaService.ImprimirTexto(config.NomeImpressora, textoCupom, config.CortarPapel);
            }
            ExibirMensagem($"✓ Comprovante da Venda #{Pedido.NumeroFormatado} reimpresso na impressora '{config.NomeImpressora}'.", true, 4000);
        }
        catch (Exception ex)
        {
            ExibirMensagem($"Erro ao reimprimir comprovante: {ex.Message}", false, 5000);
        }
    }

    public void NotificarEstadoBloqueio()
    {
        OnPropertyChanged(nameof(IsEdicaoBloqueada));
        OnPropertyChanged(nameof(IsEdicaoPermitida));
        OnPropertyChanged(nameof(TemDadosNaoSalvos));
        OnPropertyChanged(nameof(BannerBloqueioTitulo));
        OnPropertyChanged(nameof(BannerBloqueioMensagem));
    }

    // ==============================================================
    // CHECKOUT DE CONDIÇÕES DE PAGAMENTO (MODAL BALCÃO / PRÉ-VENDA)
    // ==============================================================

    public async Task AbrirCheckoutAsync()
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem("Esta venda já está concluída e não permite alteração de pagamento.", false, 4000);
            return;
        }
        LancarPagamentos = true;
        await SalvarPedidoAsync();
    }

    private async Task IniciarCheckoutModalAsync()
    {
        Pedido.Status = Pedido.IsOrcamento ? "O" : "P";
        Pedido.Venda = 0;

        IsCheckoutOpen = true;
        await _checkoutViewModel.IniciarAsync(Pedido, UsuarioLogado);
        OnCheckoutAberto?.Invoke();
    }

    public void FecharCheckout()
    {
        IsCheckoutOpen = false;
        _checkoutViewModel.CancelarCheckout();
    }

    private async Task ExecutarGravacaoPedidoAsync()
    {
        Pedido.LkUsuario = UsuarioLogado?.Controle;
        Pedido.LkVendedor = UsuarioLogado?.LkVendedor ?? Pedido.LkVendedor;
        Pedido.Vendedor = !string.IsNullOrWhiteSpace(UsuarioLogado?.NomeVendedor) 
            ? UsuarioLogado.NomeVendedor 
            : (Pedido.Vendedor ?? UsuarioLogado?.Nome);

        var salvo = await _pedidoService.SalvarAsync(Pedido);
        Pedido = salvo;
        OnPropertyChanged(nameof(Pedido));
    }

    public async Task FinalizarPedidoDiretoAsync()
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem($"A Venda #{Pedido.NumeroFormatado} já está concluída e jamais pode ser alterada pelo sistema.", false, 5000);
            return;
        }

        try
        {
            IsBusy = true;
            Pedido.Pagamentos.Clear();

            // Status no balcão: 'O' para Orçamento, 'P' para Pedido de Venda / Pré-Venda
            Pedido.Status = Pedido.IsOrcamento ? "O" : "P";
            Pedido.Venda = 0;

            await ExecutarGravacaoPedidoAsync();

            var pedidoFinalizado = Pedido;
            ProcessarImpressaoEResetar(pedidoFinalizado, troco: 0);
        }
        catch (Exception ex)
        {
            ExibirMensagem($"Erro ao gravar pedido no banco: {ex.Message}", false, 7000);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SalvarPedidoAsync()
    {
        if (IsEdicaoBloqueada)
        {
            ExibirMensagem($"A Venda #{Pedido.NumeroFormatado} já está concluída e jamais pode ser alterada pelo sistema.", false, 5000);
            return;
        }

        string acaoNome = "totalizar";

        if (Pedido.Itens.Count == 0 || Pedido.Total <= 0)
        {
            ExibirMensagem($"Adicione ao menos um item ao pedido antes de {acaoNome}.", false, 4000);
            return;
        }

        // Validação da política de Estoque Insuficiente do ERP Legado
        bool permiteEstoqueInsuficiente = await _configuracaoVendaService.PermiteEstoqueInsuficienteAsync(Pedido.IsOrcamento);
        if (!permiteEstoqueInsuficiente && UsuarioLogado?.Nivel < 2)
        {
            var itensNaoLiberados = Pedido.Itens
                .Where(i => !i.EstoqueLiberado && !_produtosEstoqueLiberados.Contains(i.LkProduto))
                .ToList();

            if (itensNaoLiberados.Count > 0)
            {
                var itensPorProduto = itensNaoLiberados.GroupBy(i => i.LkProduto);
                var itensComEstoqueInsuficiente = new System.Collections.Generic.List<string>();

                foreach (var grp in itensPorProduto)
                {
                    decimal qtdTotal = grp.Sum(i => i.Quantidade);
                    decimal estoqueAtual = await _produtoService.ObterEstoqueAtualAsync(grp.Key);
                    if (qtdTotal > estoqueAtual)
                    {
                        itensComEstoqueInsuficiente.Add($"• '{grp.First().Produto}' (Disp: {estoqueAtual:N2} | Pedido: {qtdTotal:N2})");
                    }
                }

                if (itensComEstoqueInsuficiente.Count > 0)
                {
                    string tipoNome = Pedido.IsOrcamento ? "Orçamento" : "Pré-Venda";
                    string titulo = "Liberação: Estoque Insuficiente";
                    string subtitulo = $"{tipoNome} • Totalização do Pedido";
                    string motivo = $"Existem {itensComEstoqueInsuficiente.Count} item(ns) com estoque insuficiente neste {tipoNome}.";
                    string detalhe = string.Join("\n", itensComEstoqueInsuficiente) + $"\n\nAutorização de Gerente requerida para {acaoNome}.";

                    _ = AbrirLiberacaoGerenteAsync(titulo, subtitulo, motivo, detalhe, async (gerente) =>
                    {
                        foreach (var it in Pedido.Itens)
                        {
                            it.EstoqueLiberado = true;
                            _produtosEstoqueLiberados.Add(it.LkProduto);
                        }
                        await IniciarCheckoutModalAsync();
                    });
                    return;
                }
            }
        }

        // Validação de Desconto Geral
        if (DescontoGeralPercAtual > LimiteDescontoUsuario && LimiteDescontoUsuario > 0)
        {
            if (UsuarioLogado?.Nivel >= 2)
            {
                // Gerente logado prossegue
            }
            else
            {
                string tipoNome = Pedido.IsOrcamento ? "Orçamento" : "Pré-Venda";
                string titulo = "Liberação: Desconto Geral Excedente";
                string subtitulo = $"{tipoNome} • Totalização do Pedido";
                string motivo = $"Desconto geral de {DescontoGeralPercAtual:N2}% ({Pedido.Desconto:C2}) ultrapassa o teto permitido do vendedor ({LimiteDescontoUsuario:N2}%).";
                string detalhe = $"Subtotal: {Pedido.Subtotal:C2} | Desconto Geral: {Pedido.Desconto:C2} ({DescontoGeralPercAtual:N2}%)\nInforme a senha de Gerente para autorizar.";

                _ = AbrirLiberacaoGerenteAsync(titulo, subtitulo, motivo, detalhe, async (gerente) =>
                {
                    await IniciarCheckoutModalAsync();
                });
                return;
            }
        }

        await IniciarCheckoutModalAsync();
    }
}
