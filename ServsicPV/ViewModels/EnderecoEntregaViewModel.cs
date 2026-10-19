using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using ServSic.Core.Models;
using ServSic.Core.Services;
using ServsicPV.Common;

namespace ServsicPV.ViewModels;

public class EnderecoEntregaViewModel : ViewModelBase
{
    private readonly ClienteEnderecoService _enderecoService = new();
    private readonly CepLookupService _cepService = new();

    private bool _isOpen;
    private PedidoVenda? _pedido;
    private Cliente? _cliente;

    private string _cep = string.Empty;
    private string _endereco = string.Empty;
    private string _numero = string.Empty;
    private string _complEnd = string.Empty;
    private string _bairro = string.Empty;
    private string _cidade = string.Empty;
    private string _estado = string.Empty;
    private string _obs = string.Empty;
    private string _apelidoNovo = string.Empty;

    private int? _lkEnderecoCli;
    private bool _salvarNovoDestinoNoCadastro;
    private bool _isLoading;
    private string _mensagemStatus = string.Empty;
    private ClienteEndereco? _selectedDestino;

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public PedidoVenda? Pedido => _pedido;
    public Cliente? Cliente => _cliente;
    public bool HasCliente => _cliente != null && _cliente.Controle > 0;
    public string NomeClienteExibicao => HasCliente ? $"{_cliente!.Nome} (Cód: {_cliente.Controle})" : "CONSUMIDOR / BALCÃO (Sem cadastro vinculado)";

    public ObservableCollection<ClienteEndereco> EnderecosCadastrados { get; } = new();
    public bool HasEnderecosCadastrados => EnderecosCadastrados.Count > 0;

    public ClienteEndereco? SelectedDestino
    {
        get => _selectedDestino;
        set
        {
            if (SetProperty(ref _selectedDestino, value) && value != null)
            {
                AplicarDestinoSelecionado(value);
            }
        }
    }

    public string Cep
    {
        get => _cep;
        set => SetProperty(ref _cep, value);
    }

    public string Endereco
    {
        get => _endereco;
        set => SetProperty(ref _endereco, value);
    }

    public string Numero
    {
        get => _numero;
        set => SetProperty(ref _numero, value);
    }

    public string ComplEnd
    {
        get => _complEnd;
        set => SetProperty(ref _complEnd, value);
    }

    public string Bairro
    {
        get => _bairro;
        set => SetProperty(ref _bairro, value);
    }

    public string Cidade
    {
        get => _cidade;
        set => SetProperty(ref _cidade, value);
    }

    public string Estado
    {
        get => _estado;
        set => SetProperty(ref _estado, value);
    }

    public string Obs
    {
        get => _obs;
        set => SetProperty(ref _obs, value);
    }

    public string ApelidoNovo
    {
        get => _apelidoNovo;
        set => SetProperty(ref _apelidoNovo, value);
    }

    public bool SalvarNovoDestinoNoCadastro
    {
        get => _salvarNovoDestinoNoCadastro;
        set => SetProperty(ref _salvarNovoDestinoNoCadastro, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string MensagemStatus
    {
        get => _mensagemStatus;
        set => SetProperty(ref _mensagemStatus, value);
    }

    public string EnderecoResumoFormatado
    {
        get
        {
            var p = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(Endereco))
            {
                string end = Endereco;
                if (!string.IsNullOrWhiteSpace(Numero)) end += $", {Numero}";
                p.Add(end);
            }
            if (!string.IsNullOrWhiteSpace(ComplEnd)) p.Add(ComplEnd);
            if (!string.IsNullOrWhiteSpace(Bairro)) p.Add(Bairro);
            if (!string.IsNullOrWhiteSpace(Cidade))
            {
                string cid = Cidade;
                if (!string.IsNullOrWhiteSpace(Estado)) cid += $" - {Estado}";
                p.Add(cid);
            }
            if (!string.IsNullOrWhiteSpace(Cep)) p.Add($"CEP: {Cep}");
            return p.Count > 0 ? string.Join(" - ", p) : "Nenhum endereço informado";
        }
    }

    public ICommand BuscarCepCommand { get; }
    public ICommand UsarEnderecoPrincipalCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand FecharCommand { get; }
    public ICommand AbrirGoogleMapsCommand { get; }
    public ICommand LimparEnderecoCommand { get; }

    public event Action? OnEnderecoConfirmado;

    public EnderecoEntregaViewModel()
    {
        BuscarCepCommand = new RelayCommand(async _ => await BuscarCepAsync());
        UsarEnderecoPrincipalCommand = new RelayCommand(_ => UsarEnderecoPrincipalCliente());
        ConfirmarCommand = new RelayCommand(async _ => await ConfirmarAsync());
        FecharCommand = new RelayCommand(_ => Fechar());
        AbrirGoogleMapsCommand = new RelayCommand(_ => AbrirNoGoogleMaps());
        LimparEnderecoCommand = new RelayCommand(_ => LimparCampos());
    }

    public async Task AbrirAsync(PedidoVenda pedido, Cliente? cliente)
    {
        _pedido = pedido;
        _cliente = cliente;

        OnPropertyChanged(nameof(Pedido));
        OnPropertyChanged(nameof(Cliente));
        OnPropertyChanged(nameof(HasCliente));
        OnPropertyChanged(nameof(NomeClienteExibicao));

        MensagemStatus = string.Empty;
        SalvarNovoDestinoNoCadastro = false;
        ApelidoNovo = string.Empty;

        // 1. Carrega dados já presentes no pedido se houver
        if (!string.IsNullOrWhiteSpace(pedido.Endereco))
        {
            Cep = pedido.CEP ?? string.Empty;
            Endereco = pedido.Endereco ?? string.Empty;
            Numero = pedido.Numero ?? string.Empty;
            ComplEnd = pedido.ComplEnd ?? string.Empty;
            Bairro = pedido.Bairro ?? string.Empty;
            Cidade = pedido.Cidade ?? string.Empty;
            Estado = pedido.UF ?? string.Empty;
            Obs = pedido.Obs ?? string.Empty;
            _lkEnderecoCli = pedido.LkEnderecoCli;
        }
        else if (HasCliente)
        {
            // Inicializa com endereço principal do cliente
            UsarEnderecoPrincipalCliente();
        }
        else
        {
            LimparCampos();
        }

        // 2. Se tiver cliente cadastrado, busca múltiplos endereços
        EnderecosCadastrados.Clear();
        if (HasCliente)
        {
            try
            {
                IsLoading = true;
                var lista = await _enderecoService.ListarAtivosAsync(cliente!.Controle);
                foreach (var end in lista)
                {
                    EnderecosCadastrados.Add(end);
                }

                if (_lkEnderecoCli.HasValue && _lkEnderecoCli.Value > 0)
                {
                    _selectedDestino = EnderecosCadastrados.FirstOrDefault(e => e.Controle == _lkEnderecoCli.Value);
                    OnPropertyChanged(nameof(SelectedDestino));
                }
            }
            catch (Exception ex)
            {
                MensagemStatus = $"Erro ao carregar destinos: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasEnderecosCadastrados));
            }
        }
        else
        {
            OnPropertyChanged(nameof(HasEnderecosCadastrados));
        }

        OnPropertyChanged(nameof(EnderecoResumoFormatado));
        IsOpen = true;
    }

    public void Fechar()
    {
        IsOpen = false;
    }

    private void UsarEnderecoPrincipalCliente()
    {
        if (!HasCliente) return;

        _selectedDestino = null;
        _lkEnderecoCli = null;
        OnPropertyChanged(nameof(SelectedDestino));

        Cep = _cliente!.CEP ?? string.Empty;
        Endereco = _cliente.Endereco ?? string.Empty;
        Numero = _cliente.Numero ?? string.Empty;
        ComplEnd = _cliente.ComplEnd ?? string.Empty;
        Bairro = _cliente.Bairro ?? string.Empty;
        Cidade = _cliente.Cidade ?? string.Empty;
        Estado = _cliente.Estado ?? string.Empty;

        MensagemStatus = "Endereço principal do cadastro do cliente selecionado.";
        OnPropertyChanged(nameof(EnderecoResumoFormatado));
    }

    private void AplicarDestinoSelecionado(ClienteEndereco dest)
    {
        _lkEnderecoCli = dest.Controle;
        Cep = dest.Cep ?? string.Empty;
        Endereco = dest.Endereco ?? string.Empty;
        Numero = dest.Numero ?? string.Empty;
        ComplEnd = dest.ComplEnd ?? string.Empty;
        Bairro = dest.Bairro ?? string.Empty;
        Cidade = dest.Cidade ?? string.Empty;
        Estado = dest.Estado ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(dest.Obs)) Obs = dest.Obs;

        MensagemStatus = $"Destino '{dest.Apelido}' selecionado.";
        OnPropertyChanged(nameof(EnderecoResumoFormatado));
    }

    public void LimparCampos()
    {
        _selectedDestino = null;
        _lkEnderecoCli = null;
        OnPropertyChanged(nameof(SelectedDestino));

        Cep = string.Empty;
        Endereco = string.Empty;
        Numero = string.Empty;
        ComplEnd = string.Empty;
        Bairro = string.Empty;
        Cidade = string.Empty;
        Estado = string.Empty;
        Obs = string.Empty;
        ApelidoNovo = string.Empty;
        SalvarNovoDestinoNoCadastro = false;
        MensagemStatus = string.Empty;
        OnPropertyChanged(nameof(EnderecoResumoFormatado));
    }

    public async Task BuscarCepAsync()
    {
        string digits = Regex.Replace(Cep ?? "", @"\D", "");
        if (digits.Length != 8)
        {
            MensagemStatus = "Digite um CEP válido com 8 dígitos.";
            return;
        }

        try
        {
            IsLoading = true;
            MensagemStatus = "Buscando CEP...";
            var res = await _cepService.ConsultarAsync(digits);
            if (res == null || res.Erro)
            {
                MensagemStatus = "CEP não localizado pelo serviço ViaCEP.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(res.Logradouro)) Endereco = res.Logradouro.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(res.Bairro)) Bairro = res.Bairro.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(res.Localidade)) Cidade = res.Localidade.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(res.Uf)) Estado = res.Uf.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(res.Complemento) && string.IsNullOrWhiteSpace(ComplEnd)) ComplEnd = res.Complemento.ToUpperInvariant();

            MensagemStatus = "Endereço preenchido com sucesso pelo CEP.";
            OnPropertyChanged(nameof(EnderecoResumoFormatado));
        }
        catch (Exception ex)
        {
            MensagemStatus = $"Erro ao consultar CEP: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ConfirmarAsync()
    {
        if (_pedido == null)
        {
            Fechar();
            return;
        }

        // Validação mínima se preencheu algo
        bool temDados = !string.IsNullOrWhiteSpace(Endereco) || !string.IsNullOrWhiteSpace(Cidade) || !string.IsNullOrWhiteSpace(Cep);
        if (!temDados)
        {
            // Usuário optou por limpar endereço do pedido
            _pedido.Endereco = null;
            _pedido.Numero = null;
            _pedido.ComplEnd = null;
            _pedido.Bairro = null;
            _pedido.Cidade = null;
            _pedido.UF = null;
            _pedido.CEP = null;
            _pedido.Obs = null;
            _pedido.LkEnderecoCli = null;

            Fechar();
            OnEnderecoConfirmado?.Invoke();
            return;
        }

        // Se marcou para salvar como novo destino do cliente
        if (HasCliente && SalvarNovoDestinoNoCadastro && !string.IsNullOrWhiteSpace(ApelidoNovo))
        {
            try
            {
                var novoDestino = new ClienteEndereco
                {
                    LkCliente = _cliente!.Controle,
                    Apelido = ApelidoNovo.Trim().ToUpperInvariant(),
                    Endereco = Endereco?.Trim().ToUpperInvariant(),
                    Numero = Numero?.Trim().ToUpperInvariant(),
                    ComplEnd = ComplEnd?.Trim().ToUpperInvariant(),
                    Bairro = Bairro?.Trim().ToUpperInvariant(),
                    Cidade = Cidade?.Trim().ToUpperInvariant(),
                    Estado = Estado?.Trim().ToUpperInvariant(),
                    Cep = Regex.Replace(Cep ?? "", @"\D", ""),
                    Obs = Obs?.Trim().ToUpperInvariant()
                };

                int novoId = await _enderecoService.IncluirAsync(novoDestino);
                _lkEnderecoCli = novoId;
            }
            catch (Exception ex)
            {
                MensagemStatus = $"Erro ao salvar novo destino no cadastro: {ex.Message}";
                return;
            }
        }

        // Copia os dados para a capa do pedido (TABEST3A)
        _pedido.Endereco = Endereco?.Trim().ToUpperInvariant();
        _pedido.Numero = Numero?.Trim().ToUpperInvariant();
        _pedido.ComplEnd = ComplEnd?.Trim().ToUpperInvariant();
        _pedido.Bairro = Bairro?.Trim().ToUpperInvariant();
        _pedido.Cidade = Cidade?.Trim().ToUpperInvariant();
        _pedido.UF = Estado?.Trim().ToUpperInvariant();
        _pedido.CEP = Regex.Replace(Cep ?? "", @"\D", "");
        if (!string.IsNullOrWhiteSpace(Obs)) _pedido.Obs = Obs.Trim().ToUpperInvariant();
        _pedido.LkEnderecoCli = _lkEnderecoCli;

        Fechar();
        OnEnderecoConfirmado?.Invoke();
    }

    public void AbrirNoGoogleMaps()
    {
        string endCompleto = EnderecoResumoFormatado;
        if (string.IsNullOrWhiteSpace(endCompleto) || endCompleto == "Nenhum endereço informado")
        {
            MensagemStatus = "Informe o endereço antes de abrir no Google Maps.";
            return;
        }

        try
        {
            string url = $"https://www.google.com/maps/search/?api=1&query={WebUtility.UrlEncode(endCompleto)}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MensagemStatus = $"Não foi possível abrir o navegador: {ex.Message}";
        }
    }
}
