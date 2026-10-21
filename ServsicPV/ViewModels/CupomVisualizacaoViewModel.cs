using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ServSic.Core.Models;
using ServSic.Core.Services;
using ServsicPV.Common;

namespace ServsicPV.ViewModels;

public class CupomVisualizacaoViewModel : ViewModelBase
{
    private readonly ImpressoraConfigService _configService;
    private readonly ImpressaoTermicaService _impressaoService;
    private readonly CupomFormatadorService _formatadorService;

    private bool _isOpen;
    private string _textoCupom = string.Empty;
    private string _tituloModal = "PEDIDO DE VENDA";
    private PedidoVenda? _pedidoAtual;
    private ConfiguracaoImpressora _configAtual = new();
    private bool _isBusy;
    private string _mensagemStatus = string.Empty;
    private bool _isSucesso;

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public string TextoCupom
    {
        get => _textoCupom;
        set => SetProperty(ref _textoCupom, value);
    }

    public string TituloModal
    {
        get => _tituloModal;
        set => SetProperty(ref _tituloModal, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string MensagemStatus
    {
        get => _mensagemStatus;
        set => SetProperty(ref _mensagemStatus, value);
    }

    public bool IsSucesso
    {
        get => _isSucesso;
        set => SetProperty(ref _isSucesso, value);
    }

    public ICommand ImprimirCommand { get; }
    public ICommand FecharCommand { get; }

    public Action? OnFechado { get; set; }

    public CupomVisualizacaoViewModel(
        ImpressoraConfigService? configService = null,
        ImpressaoTermicaService? impressaoService = null,
        CupomFormatadorService? formatadorService = null)
    {
        _configService = configService ?? new ImpressoraConfigService();
        _impressaoService = impressaoService ?? new ImpressaoTermicaService();
        _formatadorService = formatadorService ?? new CupomFormatadorService();

        ImprimirCommand = new RelayCommand(_ => _ = ImprimirAsync());
        FecharCommand = new RelayCommand(_ => Fechar());
    }

    public async Task CarregarAsync(PedidoVenda pedido)
    {
        _pedidoAtual = pedido;
        _configAtual = _configService.ObterConfiguracao();
        MensagemStatus = string.Empty;

        int num = pedido.Pedido > 0 ? pedido.Pedido : pedido.Controle;
        TituloModal = pedido.IsOrcamento ? $"ORÇAMENTO DE VENDA Nº {num:D6}" : $"PEDIDO DE VENDA Nº {num:D6}";

        var empresa = await _configService.ObterDadosEmpresaAsync();
        TextoCupom = _formatadorService.GerarTextoCupom(pedido, empresa, _configAtual);
        IsOpen = true;
    }

    public async Task ImprimirAsync()
    {
        if (string.IsNullOrWhiteSpace(_configAtual.NomeImpressora))
        {
            MensagemStatus = "Nenhuma impressora configurada. Acesse o botão de engrenagem para selecionar.";
            IsSucesso = false;
            return;
        }

        try
        {
            IsBusy = true;
            MensagemStatus = "Enviando para a impressora...";
            IsSucesso = true;

            int vias = _configAtual.DuasVias ? 2 : 1;
            bool sucessoGeral = true;
            string? erroGeral = null;

            for (int i = 0; i < vias; i++)
            {
                var res = await Task.Run(() => _impressaoService.ImprimirTexto(_configAtual.NomeImpressora, TextoCupom, _configAtual.CortarPapel, _configAtual.ImprimirNegrito));
                if (!res.Sucesso)
                {
                    sucessoGeral = false;
                    erroGeral = res.Mensagem;
                    break;
                }
            }

            if (sucessoGeral)
            {
                MensagemStatus = $"Documento impresso com sucesso em '{_configAtual.NomeImpressora}'!";
                IsSucesso = true;
                await Task.Delay(800);
                Fechar();
            }
            else
            {
                MensagemStatus = $"Erro ao imprimir: {erroGeral}";
                IsSucesso = false;
            }
        }
        catch (Exception ex)
        {
            MensagemStatus = $"Falha na impressão: {ex.Message}";
            IsSucesso = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Fechar()
    {
        IsOpen = false;
        OnFechado?.Invoke();
    }
}
