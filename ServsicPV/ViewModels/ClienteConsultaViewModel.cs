using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ServsicPV.Common;
using ServSic.Core.Models;
using ServSic.Core.Services;

namespace ServsicPV.ViewModels;

public class ClienteConsultaViewModel : ViewModelBase
{
    private readonly ClienteService _clienteService;
    private string _filtroTexto = string.Empty;
    private bool _isBusy;
    private string _mensagem = string.Empty;
    private Cliente? _clienteSelecionado;
    private CancellationTokenSource? _pesquisaCts;

    public ObservableCollection<Cliente> Clientes { get; } = new();

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

    public Cliente? ClienteSelecionado
    {
        get => _clienteSelecionado;
        set => SetProperty(ref _clienteSelecionado, value);
    }

    public Action<Cliente>? OnClienteSelecionado { get; set; }
    public Action? OnVoltarAoPedido { get; set; }
    public Action? OnSolicitarNovoCliente { get; set; }
    public Action? OnPesquisaConcluida { get; set; }

    public ICommand PesquisarCommand { get; }
    public ICommand ConfirmarSelecaoCommand { get; }
    public ICommand VoltarAoPedidoCommand { get; }
    public ICommand NovoClienteCommand { get; }

    public ClienteConsultaViewModel(ClienteService? clienteService = null)
    {
        _clienteService = clienteService ?? new ClienteService();

        PesquisarCommand = new RelayCommand(async _ => await PesquisarAsync());
        ConfirmarSelecaoCommand = new RelayCommand(_ => ConfirmarSelecao());
        VoltarAoPedidoCommand = new RelayCommand(_ => OnVoltarAoPedido?.Invoke());
        NovoClienteCommand = new RelayCommand(_ => OnSolicitarNovoCliente?.Invoke());
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
            var list = await _clienteService.BuscarAsync(FiltroTexto, 100);

            Clientes.Clear();
            foreach (var c in list)
            {
                Clientes.Add(c);
            }

            if (Clientes.Count > 0)
            {
                ClienteSelecionado = Clientes[0];
            }
            else
            {
                ClienteSelecionado = null;
                Mensagem = "Nenhum cliente localizado para o termo informado.";
            }

            OnPesquisaConcluida?.Invoke();
        }
        catch (Exception ex)
        {
            Mensagem = $"Erro ao pesquisar clientes: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ConfirmarSelecao()
    {
        if (ClienteSelecionado != null)
        {
            OnClienteSelecionado?.Invoke(ClienteSelecionado);
        }
    }
}
