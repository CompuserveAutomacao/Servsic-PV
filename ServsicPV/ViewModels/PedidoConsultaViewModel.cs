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
    private DateTimeOffset? _dataInicio = DateTimeOffset.Now.AddDays(-7);
    private DateTimeOffset? _dataFim = DateTimeOffset.Now;
    private bool _isBusy;
    private string _mensagem = string.Empty;
    private PedidoVenda? _pedidoSelecionado;

    public Usuario? UsuarioLogado { get; set; }

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

    public DateTimeOffset? DataInicio
    {
        get => _dataInicio;
        set => SetProperty(ref _dataInicio, value);
    }

    public DateTimeOffset? DataFim
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

    private PedidoVenda? _pedidoDetalhe;
    private bool _isCarregandoDetalhes;

    public PedidoVenda? PedidoDetalhe
    {
        get => _pedidoDetalhe;
        set
        {
            if (SetProperty(ref _pedidoDetalhe, value))
            {
                OnPropertyChanged(nameof(HasPedidoDetalhe));
                OnPropertyChanged(nameof(HasPagamentos));
                OnPropertyChanged(nameof(HasItensDetalhe));
            }
        }
    }

    public bool HasPedidoDetalhe => PedidoDetalhe != null;
    public bool HasPagamentos => PedidoDetalhe?.Pagamentos?.Count > 0;
    public bool HasItensDetalhe => PedidoDetalhe?.Itens?.Count > 0;

    private string _abaDetalhes = "PRODUTOS";
    public string AbaDetalhes
    {
        get => _abaDetalhes;
        set
        {
            if (SetProperty(ref _abaDetalhes, value))
            {
                OnPropertyChanged(nameof(IsAbaProdutos));
                OnPropertyChanged(nameof(IsAbaPagamentos));
            }
        }
    }

    public bool IsAbaProdutos => AbaDetalhes == "PRODUTOS";
    public bool IsAbaPagamentos => AbaDetalhes == "PAGAMENTOS";

    public bool IsCarregandoDetalhes
    {
        get => _isCarregandoDetalhes;
        set => SetProperty(ref _isCarregandoDetalhes, value);
    }

    public PedidoVenda? PedidoSelecionado
    {
        get => _pedidoSelecionado;
        set
        {
            if (SetProperty(ref _pedidoSelecionado, value))
            {
                _ = CarregarDetalhesAsync(value);
            }
        }
    }

    public async Task CarregarDetalhesAsync(PedidoVenda? pedido)
    {
        if (pedido == null)
        {
            PedidoDetalhe = null;
            return;
        }

        try
        {
            IsCarregandoDetalhes = true;
            var completo = await _pedidoService.ObterPorIdAsync(pedido.Controle);
            PedidoDetalhe = completo ?? pedido;
        }
        catch
        {
            PedidoDetalhe = pedido;
        }
        finally
        {
            IsCarregandoDetalhes = false;
        }
    }

    private bool _isConfirmacaoExclusaoOpen;
    private string _tituloConfirmacaoExclusao = string.Empty;
    private string _mensagemConfirmacaoExclusao = string.Empty;
    private string _tipoExclusaoPendente = string.Empty;

    public bool IsConfirmacaoExclusaoOpen
    {
        get => _isConfirmacaoExclusaoOpen;
        set => SetProperty(ref _isConfirmacaoExclusaoOpen, value);
    }

    public string TituloConfirmacaoExclusao
    {
        get => _tituloConfirmacaoExclusao;
        set => SetProperty(ref _tituloConfirmacaoExclusao, value);
    }

    public string MensagemConfirmacaoExclusao
    {
        get => _mensagemConfirmacaoExclusao;
        set => SetProperty(ref _mensagemConfirmacaoExclusao, value);
    }

    public Action<int>? OnEditarPedido { get; set; }

    public ICommand PesquisarCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand ConverterVendaCommand { get; }
    public ICommand SelecionarAbaCommand { get; }
    public ICommand SolicitarExcluirSelecionadoCommand { get; }
    public ICommand SolicitarExcluirPendentesCommand { get; }
    public ICommand ConfirmarExclusaoCommand { get; }
    public ICommand CancelarExclusaoCommand { get; }
    public ICommand ImprimirCommand { get; }

    public PedidoConsultaViewModel(PedidoService? pedidoService = null)
    {
        _pedidoService = pedidoService ?? new PedidoService();

        PesquisarCommand = new RelayCommand(async _ => await PesquisarAsync());
        EditarCommand = new RelayCommand<PedidoVenda>(p => Editar(p));
        ConverterVendaCommand = new RelayCommand<PedidoVenda>(async p => await ConverterVendaAsync(p));
        SelecionarAbaCommand = new RelayCommand<string>(aba => { if (!string.IsNullOrEmpty(aba)) AbaDetalhes = aba; });

        SolicitarExcluirSelecionadoCommand = new RelayCommand(_ => SolicitarExcluirSelecionado());
        SolicitarExcluirPendentesCommand = new RelayCommand(_ => SolicitarExcluirPendentes());
        ConfirmarExclusaoCommand = new RelayCommand(async _ => await ExecutarExclusaoConfirmadaAsync());
        CancelarExclusaoCommand = new RelayCommand(_ => IsConfirmacaoExclusaoOpen = false);
        ImprimirCommand = new RelayCommand(async _ => await ImprimirPedidoSelecionadoAsync());

        _ = PesquisarAsync();
    }

    private void SolicitarExcluirSelecionado()
    {
        var item = PedidoSelecionado ?? PedidoDetalhe;
        if (item == null)
        {
            Mensagem = "Nenhum pedido ou orçamento selecionado para exclusão.";
            return;
        }

        // Trava de segurança: Pedidos já fechados ou concluídos possuem faturamento e quitação
        if (item.SituacaoDescricao == "FECHADO" || item.SituacaoDescricao == "CONCLUIDA")
        {
            Mensagem = $"O Pedido/Orçamento Nº {item.Pedido} já está {item.SituacaoDescricao} e possui faturamento/pagamento. Não é permitido excluir pedidos finalizados.";
            return;
        }

        _tipoExclusaoPendente = "SELECIONADO";
        TituloConfirmacaoExclusao = "Excluir Pedido / Orçamento";
        MensagemConfirmacaoExclusao = $"Deseja realmente excluir em definitivo o pedido Nº {item.Pedido} ({item.OperacaoDescricao}) do cliente '{item.Cliente}'? Esta ação apagará permanentemente o cabeçalho, itens e pagamentos das tabelas do banco de dados.";
        IsConfirmacaoExclusaoOpen = true;
    }

    private void SolicitarExcluirPendentes()
    {
        _tipoExclusaoPendente = "PENDENTES";
        TituloConfirmacaoExclusao = "Excluir Todos os Pendentes";
        MensagemConfirmacaoExclusao = "Deseja realmente excluir em definitivo TODOS os orçamentos e pré-vendas PENDENTES (em aberto e sem formas de pagamento) vinculados ao seu usuário? Pedidos já fechados ou concluídos não serão alterados.";
        IsConfirmacaoExclusaoOpen = true;
    }

    private async Task ExecutarExclusaoConfirmadaAsync()
    {
        IsConfirmacaoExclusaoOpen = false;
        try
        {
            IsBusy = true;
            if (_tipoExclusaoPendente == "SELECIONADO")
            {
                var item = PedidoSelecionado ?? PedidoDetalhe;
                if (item != null)
                {
                    await _pedidoService.ExcluirPedidoCompletoAsync(item.Controle, item.Pedido);
                    Mensagem = $"Pedido/Orçamento Nº {item.Pedido} excluído com sucesso das tabelas do banco.";
                    await PesquisarAsync();
                }
            }
            else if (_tipoExclusaoPendente == "PENDENTES")
            {
                int totalExcluidos = await _pedidoService.ExcluirPendentesAsync(UsuarioLogado?.Controle, UsuarioLogado?.LkVendedor);
                Mensagem = totalExcluidos > 0 
                    ? $"{totalExcluidos} pedido(s) pendente(s) excluído(s) com sucesso das tabelas." 
                    : "Nenhum pedido pendente encontrado para exclusão.";
                await PesquisarAsync();
            }
        }
        catch (Exception ex)
        {
            Mensagem = $"Erro ao excluir: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ImprimirPedidoSelecionadoAsync()
    {
        var item = PedidoSelecionado ?? PedidoDetalhe;
        if (item == null)
        {
            Mensagem = "Selecione um pedido ou orçamento para imprimir.";
            return;
        }

        try
        {
            IsBusy = true;
            var completo = await _pedidoService.ObterPorIdAsync(item.Controle) ?? item;
            var configService = new ImpressoraConfigService();
            var config = configService.ObterConfiguracao();

            if (string.IsNullOrWhiteSpace(config.NomeImpressora))
            {
                Mensagem = "Nenhuma impressora configurada no sistema. Acesse as configurações no topo para definir a impressora.";
                return;
            }

            var empresa = await configService.ObterDadosEmpresaAsync();
            var formatador = new CupomFormatadorService();
            string textoCupom = formatador.GerarTextoCupom(completo, empresa, config);

            var termica = new ImpressaoTermicaService();
            int vias = config.DuasVias ? 2 : 1;
            for (int i = 0; i < vias; i++)
            {
                termica.ImprimirTexto(config.NomeImpressora, textoCupom, config.CortarPapel);
            }

            Mensagem = $"Comprovante do Pedido Nº {completo.Pedido} enviado para a impressora '{config.NomeImpressora}'.";
        }
        catch (Exception ex)
        {
            Mensagem = $"Erro ao imprimir comprovante: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Abrir(Usuario? usuario = null)
    {
        if (usuario != null) UsuarioLogado = usuario;
        DataInicio = DateTimeOffset.Now.AddDays(-7);
        DataFim = DateTimeOffset.Now;
        _ = PesquisarAsync();
    }

    public void AbrirVendaSelecionada()
    {
        var item = PedidoSelecionado ?? PedidoDetalhe;
        if (item != null)
        {
            Editar(item);
        }
    }

    public async Task PesquisarAsync()
    {
        try
        {
            IsBusy = true;
            Mensagem = string.Empty;
            Pedidos.Clear();

            string? statusParam = FiltroStatus?.Trim().ToUpperInvariant() switch
            {
                "ORÇAMENTOS" or "ORCAMENTOS" => "O",
                "PRÉ-VENDAS" or "PRE-VENDAS" or "PRÉ-VENDA" or "PRE-VENDA" => "P",
                "VENDAS" or "VENDA" => "V",
                "CANCELADOS" or "CANCELADO" => "C",
                _ => null
            };

            int? lkUsuario = UsuarioLogado?.Controle;
            int? lkVendedor = UsuarioLogado?.LkVendedor;

            var lista = await _pedidoService.ListarAsync(
                statusParam,
                FiltroTexto,
                DataInicio?.DateTime,
                DataFim?.DateTime,
                250,
                lkUsuario,
                lkVendedor
            );

            foreach (var p in lista)
            {
                Pedidos.Add(p);
            }

            if (Pedidos.Count > 0)
            {
                PedidoSelecionado = Pedidos[0];
            }
            else
            {
                PedidoSelecionado = null;
                PedidoDetalhe = null;
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
