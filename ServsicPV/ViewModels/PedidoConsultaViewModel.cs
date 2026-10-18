using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ServsicPV.Common;
using ServSic.Core.Models;
using ServSic.Core.Services;

namespace ServsicPV.ViewModels;

public class PedidoConsultaViewModel : ViewModelBase
{
    private readonly PedidoService _pedidoService;
    private string _filtroTexto = string.Empty;
    private string _filtroStatus = "TODOS";
    private DateTimeOffset _dataInicio = DateTimeOffset.Now.AddDays(-30);
    private DateTimeOffset _dataFim = DateTimeOffset.Now;
    private bool _isBusy;
    private string _mensagem = string.Empty;
    private PedidoVenda? _pedidoSelecionado;

    public ObservableCollection<PedidoVenda> Pedidos { get; } = new();

    public string FiltroTexto
    {
        get => _filtroTexto;
        set => SetProperty(ref _filtroTexto, value);
    }

    public string FiltroStatus
    {
        get => _filtroStatus;
        set
        {
            if (SetProperty(ref _filtroStatus, value))
            {
                _ = PesquisarAsync();
            }
        }
    }

    public DateTimeOffset DataInicio
    {
        get => _dataInicio;
        set => SetProperty(ref _dataInicio, value);
    }

    public DateTimeOffset DataFim
    {
        get => _dataFim;
        set => SetProperty(ref _dataFim, value);
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

    public PedidoVenda? PedidoSelecionado
    {
        get => _pedidoSelecionado;
        set => SetProperty(ref _pedidoSelecionado, value);
    }

    public Action<int>? OnEditarPedido { get; set; }

    public ICommand PesquisarCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand ConverterVendaCommand { get; }

    public PedidoConsultaViewModel(PedidoService? pedidoService = null)
    {
        _pedidoService = pedidoService ?? new PedidoService();

        PesquisarCommand = new RelayCommand(async _ => await PesquisarAsync());
        EditarCommand = new RelayCommand<PedidoVenda>(p => Editar(p));
        ConverterVendaCommand = new RelayCommand<PedidoVenda>(async p => await ConverterVendaAsync(p));

        _ = PesquisarAsync();
    }

    public async Task PesquisarAsync()
    {
        try
        {
            IsBusy = true;
            Mensagem = string.Empty;
            Pedidos.Clear();

            string? statusParam = FiltroStatus switch
            {
                "ORÇAMENTOS" => "O",
                "VENDAS" => "V",
                _ => null
            };

            var lista = await _pedidoService.ListarAsync(
                statusParam,
                FiltroTexto,
                DataInicio.DateTime,
                DataFim.DateTime,
                150
            );

            foreach (var p in lista)
            {
                Pedidos.Add(p);
            }

            if (Pedidos.Count == 0)
            {
                Mensagem = "Nenhum pedido ou orçamento encontrado com os filtros aplicados.";
            }
        }
        catch (Exception ex)
        {
            Mensagem = $"Erro ao pesquisar pedidos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Editar(PedidoVenda? p)
    {
        var item = p ?? PedidoSelecionado;
        if (item != null)
        {
            OnEditarPedido?.Invoke(item.Controle);
        }
    }

    private async Task ConverterVendaAsync(PedidoVenda? p)
    {
        var item = p ?? PedidoSelecionado;
        if (item == null || item.Status != "O") return;

        try
        {
            IsBusy = true;
            bool ok = await _pedidoService.ConverterOrcamentoEmVendaAsync(item.Controle);
            if (ok)
            {
                item.Status = "V";
                item.Venda = 1;
                Mensagem = $"Orçamento #{item.Pedido} convertido para Venda com sucesso!";
                await PesquisarAsync();
            }
        }
        catch (Exception ex)
        {
            Mensagem = $"Erro ao converter: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
