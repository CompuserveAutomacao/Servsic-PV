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
    private bool _isBusy;
    private string _mensagem = string.Empty;
    private string _totalEncontrados = string.Empty;
    private Produto? _produtoSelecionado;
    private BitmapImage? _fotoBitmap;
    private CancellationTokenSource? _pesquisaCts;

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
                _ = CarregarFotoProdutoAsync(value);
            }
        }
    }

    public bool HasProdutoSelecionado => ProdutoSelecionado != null;

    public BitmapImage? FotoBitmap
    {
        get => _fotoBitmap;
        set => SetProperty(ref _fotoBitmap, value);
    }

    public Action<Produto>? OnProdutoSelecionado { get; set; }
    public Action? OnVoltarAoPedido { get; set; }
    public Action? OnPesquisaConcluida { get; set; }

    public ICommand PesquisarCommand { get; }
    public ICommand ConfirmarSelecaoCommand { get; }
    public ICommand VoltarAoPedidoCommand { get; }
    public ICommand LimparFiltroCommand { get; }

    public ProdutoConsultaViewModel(ProdutoService? produtoService = null)
    {
        _produtoService = produtoService ?? new ProdutoService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        PesquisarCommand = new RelayCommand(async _ => await PesquisarAsync());
        ConfirmarSelecaoCommand = new RelayCommand(_ => ConfirmarSelecao());
        VoltarAoPedidoCommand = new RelayCommand(_ => OnVoltarAoPedido?.Invoke());
        LimparFiltroCommand = new RelayCommand(_ => FiltroTexto = string.Empty);
    }

    public void Abrir(string? filtroInicial = null)
    {
        _filtroTexto = filtroInicial ?? string.Empty;
        OnPropertyChanged(nameof(FiltroTexto));
        _ = PesquisarAsync();
    }

    private async Task DispararPesquisaAutomaticaAsync()
    {
        _pesquisaCts?.Cancel();
        _pesquisaCts?.Dispose();
        _pesquisaCts = new CancellationTokenSource();
        var token = _pesquisaCts.Token;

        try
        {
            await Task.Delay(250, token);
            if (token.IsCancellationRequested) return;

            await PesquisarAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }

    public async Task PesquisarAsync()
    {
        try
        {
            IsBusy = true;
            Mensagem = string.Empty;
            var list = await _produtoService.BuscarAsync(FiltroTexto, 120);

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
        catch (Exception ex)
        {
            Mensagem = $"Erro ao pesquisar produtos: {ex.Message}";
            TotalEncontrados = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CarregarFotoProdutoAsync(Produto? produto)
    {
        if (produto == null || (!produto.HasFoto))
        {
            FotoBitmap = null;
            return;
        }

        try
        {
            if (produto.FotoBytes != null && produto.FotoBytes.Length > 0)
            {
                using var stream = new InMemoryRandomAccessStream();
                using var writer = new DataWriter(stream);
                writer.WriteBytes(produto.FotoBytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                _dispatcherQueue.TryEnqueue(() => FotoBitmap = bitmap);
                return;
            }

            if (!string.IsNullOrWhiteSpace(produto.FotoPath) && File.Exists(produto.FotoPath))
            {
                var bitmap = new BitmapImage(new Uri(produto.FotoPath));
                _dispatcherQueue.TryEnqueue(() => FotoBitmap = bitmap);
                return;
            }
        }
        catch
        {
            FotoBitmap = null;
        }

        FotoBitmap = null;
    }

    public void ConfirmarSelecao()
    {
        if (ProdutoSelecionado != null)
        {
            OnProdutoSelecionado?.Invoke(ProdutoSelecionado);
        }
    }
}
