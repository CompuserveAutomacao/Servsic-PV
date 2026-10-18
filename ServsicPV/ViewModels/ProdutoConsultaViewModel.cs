using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using ServsicPV.Common;
using ServSic.Core.Models;
using ServSic.Core.Services;
using Windows.Storage.Streams;

namespace ServsicPV.ViewModels;

public class ProdutoConsultaViewModel : ViewModelBase
{
    private readonly ProdutoService _produtoService;
    private readonly DispatcherQueue _dispatcherQueue;

    private string _filtroTexto = string.Empty;
    private bool _isFiltrosExpandidos;
    private bool _isBusy;
    private string _mensagem = string.Empty;
    private string _totalEncontrados = string.Empty;
    private Produto? _produtoSelecionado;
    private Produto? _produtoDetalhe;
    private BitmapImage? _fotoBitmap;
    private CancellationTokenSource? _pesquisaCts;
    private CancellationTokenSource? _detalheCts;

    public ObservableCollection<Produto> Produtos { get; } = new();

    public string FiltroTexto
    {
        get => _filtroTexto;
        set
        {
            if (SetProperty(ref _filtroTexto, value))
            {
                _ = DispararPesquisaAutomaticaAsync();
            }
        }
    }

    public const string TodasMarcas = "Todas as marcas";
    public const string TodosGrupos = "Todos os grupos";
    public const string TodasLocalizacoes = "Todas as localizações";

    public ObservableCollection<string> MarcasDisponiveis { get; } = new() { TodasMarcas };
    public ObservableCollection<string> GruposDisponiveis { get; } = new() { TodosGrupos };
    public ObservableCollection<string> LocalizacoesDisponiveis { get; } = new() { TodasLocalizacoes };

    private string _filtroMarca = TodasMarcas;
    private string _filtroGrupo = TodosGrupos;
    private string _filtroLocalizacao = TodasLocalizacoes;

    public string FiltroMarca
    {
        get => _filtroMarca;
        set
        {
            if (SetProperty(ref _filtroMarca, value ?? TodasMarcas))
            {
                OnPropertyChanged(nameof(HasFiltrosAvancados));
                _ = DispararPesquisaAutomaticaAsync();
            }
        }
    }

    public string FiltroGrupo
    {
        get => _filtroGrupo;
        set
        {
            if (SetProperty(ref _filtroGrupo, value ?? TodosGrupos))
            {
                OnPropertyChanged(nameof(HasFiltrosAvancados));
                _ = DispararPesquisaAutomaticaAsync();
            }
        }
    }

    public string FiltroLocalizacao
    {
        get => _filtroLocalizacao;
        set
        {
            if (SetProperty(ref _filtroLocalizacao, value ?? TodasLocalizacoes))
            {
                OnPropertyChanged(nameof(HasFiltrosAvancados));
                _ = DispararPesquisaAutomaticaAsync();
            }
        }
    }

    public bool IsFiltrosExpandidos
    {
        get => _isFiltrosExpandidos;
        set => SetProperty(ref _isFiltrosExpandidos, value);
    }

    public bool HasFiltrosAvancados =>
        (!string.IsNullOrWhiteSpace(FiltroMarca) && FiltroMarca != TodasMarcas) ||
        (!string.IsNullOrWhiteSpace(FiltroGrupo) && FiltroGrupo != TodosGrupos) ||
        (!string.IsNullOrWhiteSpace(FiltroLocalizacao) && FiltroLocalizacao != TodasLocalizacoes);

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string Mensagem
    {
        get => _mensagem;
        set => SetProperty(ref _mensagem, value);
    }

    public string TotalEncontrados
    {
        get => _totalEncontrados;
        set => SetProperty(ref _totalEncontrados, value);
    }

    public Produto? ProdutoSelecionado
    {
        get => _produtoSelecionado;
        set
        {
            if (SetProperty(ref _produtoSelecionado, value))
            {
                OnPropertyChanged(nameof(HasProdutoSelecionado));
                ProdutoDetalhe = value;
                _ = CarregarDetalhesEFotoAsync(value);
            }
        }
    }

    public Produto? ProdutoDetalhe
    {
        get => _produtoDetalhe ?? _produtoSelecionado;
        set
        {
            if (SetProperty(ref _produtoDetalhe, value))
            {
                OnPropertyChanged(nameof(HasProdutoDetalhe));
            }
        }
    }

    public bool HasProdutoSelecionado => ProdutoSelecionado != null;
    public bool HasProdutoDetalhe => ProdutoDetalhe != null;

    public BitmapImage? FotoBitmap
    {
        get => _fotoBitmap;
        set
        {
            if (SetProperty(ref _fotoBitmap, value))
            {
                OnPropertyChanged(nameof(HasFoto));
            }
        }
    }

    public bool HasFoto => FotoBitmap != null;

    public Action<Produto>? OnProdutoSelecionado { get; set; }
    public Action? OnVoltarAoPedido { get; set; }
    public Action? OnPesquisaConcluida { get; set; }

    public ICommand PesquisarCommand { get; }
    public ICommand ConfirmarSelecaoCommand { get; }
    public ICommand VoltarAoPedidoCommand { get; }
    public ICommand LimparFiltroCommand { get; }
    public ICommand AlternarFiltrosCommand { get; }
    public ICommand LimparFiltrosAvancadosCommand { get; }

    private int _searchIdCounter;
    private string _ultimoFiltroPesquisado = string.Empty;
    public string UltimoFiltroPesquisado => _ultimoFiltroPesquisado;

    public ProdutoConsultaViewModel(ProdutoService? produtoService = null)
    {
        _produtoService = produtoService ?? new ProdutoService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        PesquisarCommand = new RelayCommand(async _ => await PesquisarImediatoAsync());
        ConfirmarSelecaoCommand = new RelayCommand(_ => ConfirmarSelecao());
        VoltarAoPedidoCommand = new RelayCommand(_ => OnVoltarAoPedido?.Invoke());
        LimparFiltroCommand = new RelayCommand(_ => FiltroTexto = string.Empty);
        AlternarFiltrosCommand = new RelayCommand(_ => IsFiltrosExpandidos = !IsFiltrosExpandidos);
        LimparFiltrosAvancadosCommand = new RelayCommand(_ => LimparFiltrosAvancados());
    }

    public void LimparFiltrosAvancados()
    {
        _filtroMarca = TodasMarcas;
        _filtroGrupo = TodosGrupos;
        _filtroLocalizacao = TodasLocalizacoes;
        OnPropertyChanged(nameof(FiltroMarca));
        OnPropertyChanged(nameof(FiltroGrupo));
        OnPropertyChanged(nameof(FiltroLocalizacao));
        OnPropertyChanged(nameof(HasFiltrosAvancados));
        _ = PesquisarImediatoAsync();
    }

    public void Abrir(string? filtroInicial = null)
    {
        _filtroTexto = filtroInicial ?? string.Empty;
        OnPropertyChanged(nameof(FiltroTexto));
        _ = CarregarFiltrosDisponiveisAsync();
        _ = PesquisarImediatoAsync();
    }

    public async Task CarregarFiltrosDisponiveisAsync()
    {
        try
        {
            var marcas = await _produtoService.ObterMarcasDisponiveisAsync();
            var grupos = await _produtoService.ObterGruposDisponiveisAsync();
            var locs = await _produtoService.ObterLocalizacoesDisponiveisAsync();

            _dispatcherQueue?.TryEnqueue(() =>
            {
                string selMarca = _filtroMarca;
                string selGrupo = _filtroGrupo;
                string selLoc = _filtroLocalizacao;

                MarcasDisponiveis.Clear();
                MarcasDisponiveis.Add(TodasMarcas);
                foreach (var m in marcas) MarcasDisponiveis.Add(m);

                GruposDisponiveis.Clear();
                GruposDisponiveis.Add(TodosGrupos);
                foreach (var g in grupos) GruposDisponiveis.Add(g);

                LocalizacoesDisponiveis.Clear();
                LocalizacoesDisponiveis.Add(TodasLocalizacoes);
                foreach (var l in locs) LocalizacoesDisponiveis.Add(l);

                _filtroMarca = MarcasDisponiveis.Contains(selMarca) ? selMarca : TodasMarcas;
                _filtroGrupo = GruposDisponiveis.Contains(selGrupo) ? selGrupo : TodosGrupos;
                _filtroLocalizacao = LocalizacoesDisponiveis.Contains(selLoc) ? selLoc : TodasLocalizacoes;
                OnPropertyChanged(nameof(FiltroMarca));
                OnPropertyChanged(nameof(FiltroGrupo));
                OnPropertyChanged(nameof(FiltroLocalizacao));
            });
        }
        catch { }
    }

    private async Task DispararPesquisaAutomaticaAsync()
    {
        _pesquisaCts?.Cancel();
        _pesquisaCts?.Dispose();
        _pesquisaCts = new CancellationTokenSource();
        var token = _pesquisaCts.Token;

        try
        {
            await Task.Delay(200, token);
            if (token.IsCancellationRequested) return;

            await ExecutarPesquisaAsync(FiltroTexto, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task PesquisarImediatoAsync()
    {
        _pesquisaCts?.Cancel();
        _pesquisaCts?.Dispose();
        _pesquisaCts = new CancellationTokenSource();
        await ExecutarPesquisaAsync(FiltroTexto, _pesquisaCts.Token);
    }

    public async Task PesquisarAsync()
    {
        await PesquisarImediatoAsync();
    }

    private async Task ExecutarPesquisaAsync(string termo, CancellationToken token)
    {
        int searchId = Interlocked.Increment(ref _searchIdCounter);

        try
        {
            IsBusy = true;
            Mensagem = string.Empty;

            string? marcaParam = FiltroMarca != TodasMarcas ? FiltroMarca : null;
            string? grupoParam = FiltroGrupo != TodosGrupos ? FiltroGrupo : null;
            string? locParam = FiltroLocalizacao != TodasLocalizacoes ? FiltroLocalizacao : null;

            var list = await _produtoService.BuscarAsync(
                termo: termo,
                marca: marcaParam,
                grupo: grupoParam,
                localizacao: locParam,
                limite: 100,
                cancellationToken: token);

            if (token.IsCancellationRequested || searchId != _searchIdCounter) return;

            _ultimoFiltroPesquisado = termo;

            Produtos.Clear();
            foreach (var p in list)
            {
                Produtos.Add(p);
            }

            if (Produtos.Count > 0)
            {
                ProdutoSelecionado = Produtos[0];
                TotalEncontrados = $"{Produtos.Count} produto(s) exibido(s)";
            }
            else
            {
                ProdutoSelecionado = null;
                TotalEncontrados = "Nenhum produto encontrado";
                Mensagem = "Nenhum produto localizado para os termos informados.";
            }

            OnPesquisaConcluida?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (searchId == _searchIdCounter)
            {
                Mensagem = $"Erro ao pesquisar produtos: {ex.Message}";
                TotalEncontrados = string.Empty;
            }
        }
        finally
        {
            if (searchId == _searchIdCounter)
            {
                IsBusy = false;
            }
        }
    }

    private async Task CarregarFotoProdutoAsync(Produto? produto)
    {
        _dispatcherQueue.TryEnqueue(() => FotoBitmap = null);

        if (produto == null)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(produto.FotoPath) && File.Exists(produto.FotoPath))
            {
                var bitmap = new BitmapImage(new Uri(produto.FotoPath));
                _dispatcherQueue.TryEnqueue(() => FotoBitmap = bitmap);
                return;
            }

            var (bytes, path) = await _produtoService.ObterInfoFotoAsync(produto.Controle);
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                produto.FotoPath = path;
                var bitmap = new BitmapImage(new Uri(path));
                _dispatcherQueue.TryEnqueue(() => FotoBitmap = bitmap);
                return;
            }

            if (bytes != null && bytes.Length > 0)
            {
                produto.FotoBytes = bytes;
                using var stream = new InMemoryRandomAccessStream();
                using var writer = new DataWriter(stream);
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                _dispatcherQueue.TryEnqueue(() => FotoBitmap = bitmap);
                return;
            }
        }
        catch
        {
            _dispatcherQueue.TryEnqueue(() => FotoBitmap = null);
        }

        _dispatcherQueue.TryEnqueue(() => FotoBitmap = null);
    }

    private async Task CarregarDetalhesEFotoAsync(Produto? produto)
    {
        if (produto == null)
        {
            FotoBitmap = null;
            return;
        }

        _detalheCts?.Cancel();
        _detalheCts?.Dispose();
        _detalheCts = new CancellationTokenSource();
        var token = _detalheCts.Token;

        try
        {
            // Busca pontual dos dados completos da Ficha Técnica (apenas 1 registro sob demanda!)
            var completo = await _produtoService.ObterPorIdAsync(produto.Controle);
            if (!token.IsCancellationRequested && completo != null)
            {
                if (_produtoSelecionado?.Controle == completo.Controle)
                {
                    _dispatcherQueue.TryEnqueue(() => ProdutoDetalhe = completo);
                }
            }
        }
        catch
        {
        }

        if (token.IsCancellationRequested) return;

        // Carrega a foto pontual do produto
        await CarregarFotoProdutoAsync(produto);
    }

    public void ConfirmarSelecao()
    {
        var prod = ProdutoDetalhe ?? ProdutoSelecionado;
        if (prod != null)
        {
            OnProdutoSelecionado?.Invoke(prod);
        }
    }
}
