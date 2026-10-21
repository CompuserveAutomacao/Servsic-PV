using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ServSic.Core.Models;
using ServSic.Core.Services;
using ServsicPV.Common;

namespace ServsicPV.ViewModels;

public class ConfiguracaoImpressoraViewModel : ViewModelBase
{
    private readonly ImpressoraConfigService _configService;
    private readonly ImpressaoTermicaService _impressaoService;

    private bool _isOpen;
    private string _selectedImpressora = string.Empty;
    private int _larguraColunas = 42;
    private string _modoImpressao = "Previa";
    private bool _cortarPapel = true;
    private bool _imprimirNegrito = true;
    private bool _duasVias = false;
    private string _mensagemRodape = "OBRIGADO PELA PREFERENCIA!";
    private string _statusMensagem = string.Empty;
    private bool _isSucesso;
    private bool _isBusy;

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public ObservableCollection<string> ImpressorasDisponiveis { get; } = new();

    public string SelectedImpressora
    {
        get => _selectedImpressora;
        set => SetProperty(ref _selectedImpressora, value);
    }

    public int LarguraColunas
    {
        get => _larguraColunas;
        set
        {
            if (SetProperty(ref _larguraColunas, value))
            {
                OnPropertyChanged(nameof(IsBobina42));
                OnPropertyChanged(nameof(IsBobina40));
                OnPropertyChanged(nameof(IsBobina48));
                OnPropertyChanged(nameof(IsBobina32));
                OnPropertyChanged(nameof(IsBobina80mm));
                OnPropertyChanged(nameof(IsBobina80mm42));
                OnPropertyChanged(nameof(IsBobina80mm40));
                OnPropertyChanged(nameof(IsBobina58mm));
            }
        }
    }

    public bool IsBobina42
    {
        get => LarguraColunas == 42 || LarguraColunas <= 0;
        set { if (value) LarguraColunas = 42; }
    }

    public bool IsBobina40
    {
        get => LarguraColunas == 40;
        set { if (value) LarguraColunas = 40; }
    }

    public bool IsBobina48
    {
        get => LarguraColunas == 48;
        set { if (value) LarguraColunas = 48; }
    }

    public bool IsBobina32
    {
        get => LarguraColunas == 32;
        set { if (value) LarguraColunas = 32; }
    }

    public bool IsBobina80mm
    {
        get => LarguraColunas >= 40;
        set { if (value) LarguraColunas = 42; }
    }

    public bool IsBobina80mm42
    {
        get => LarguraColunas == 42 || LarguraColunas <= 0;
        set { if (value) LarguraColunas = 42; }
    }

    public bool IsBobina80mm40
    {
        get => LarguraColunas == 40;
        set { if (value) LarguraColunas = 40; }
    }

    public bool IsBobina58mm
    {
        get => LarguraColunas < 40;
        set { if (value) LarguraColunas = 32; }
    }

    public string ModoImpressao
    {
        get => _modoImpressao;
        set
        {
            if (SetProperty(ref _modoImpressao, value))
            {
                OnPropertyChanged(nameof(IsModoPrevia));
                OnPropertyChanged(nameof(IsModoDireto));
                OnPropertyChanged(nameof(IsModoNaoImprimir));
            }
        }
    }

    public bool IsModoPrevia
    {
        get => ModoImpressao == "Previa";
        set { if (value) ModoImpressao = "Previa"; }
    }

    public bool IsModoDireto
    {
        get => ModoImpressao == "Direto";
        set { if (value) ModoImpressao = "Direto"; }
    }

    public bool IsModoNaoImprimir
    {
        get => ModoImpressao == "NaoImprimir";
        set { if (value) ModoImpressao = "NaoImprimir"; }
    }

    public bool CortarPapel
    {
        get => _cortarPapel;
        set => SetProperty(ref _cortarPapel, value);
    }

    public bool ImprimirNegrito
    {
        get => _imprimirNegrito;
        set => SetProperty(ref _imprimirNegrito, value);
    }

    public bool DuasVias
    {
        get => _duasVias;
        set => SetProperty(ref _duasVias, value);
    }

    public string MensagemRodape
    {
        get => _mensagemRodape;
        set => SetProperty(ref _mensagemRodape, value);
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

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand SalvarCommand { get; }
    public ICommand ImprimirTesteCommand { get; }
    public ICommand AtualizarListaCommand { get; }
    public ICommand FecharCommand { get; }

    public Action? OnFechado { get; set; }

    public ConfiguracaoImpressoraViewModel(ImpressoraConfigService? configService = null, ImpressaoTermicaService? impressaoService = null)
    {
        _configService = configService ?? new ImpressoraConfigService();
        _impressaoService = impressaoService ?? new ImpressaoTermicaService();

        SalvarCommand = new RelayCommand(_ => Salvar());
        ImprimirTesteCommand = new RelayCommand(_ => _ = ImprimirTesteAsync());
        AtualizarListaCommand = new RelayCommand(_ => CarregarImpressoras());
        FecharCommand = new RelayCommand(_ => Fechar());
    }

    public void Abrir()
    {
        CarregarImpressoras();
        var config = _configService.ObterConfiguracao();

        SelectedImpressora = config.NomeImpressora;
        LarguraColunas = config.LarguraColunas > 0 ? config.LarguraColunas : 42;
        ModoImpressao = config.ModoImpressao;
        CortarPapel = config.CortarPapel;
        ImprimirNegrito = config.ImprimirNegrito;
        DuasVias = config.DuasVias;
        MensagemRodape = config.MensagemRodape;
        StatusMensagem = string.Empty;

        // Se não houver impressora selecionada mas houver impressoras no Windows, seleciona a primeira ou padrão
        if (string.IsNullOrWhiteSpace(SelectedImpressora) && ImpressorasDisponiveis.Count > 0)
        {
            SelectedImpressora = ImpressorasDisponiveis[0];
        }

        IsOpen = true;
    }

    public void CarregarImpressoras()
    {
        ImpressorasDisponiveis.Clear();
        var lista = _impressaoService.ListarImpressorasInstaladas();

        foreach (var p in lista)
        {
            ImpressorasDisponiveis.Add(p);
        }

        if (!string.IsNullOrWhiteSpace(SelectedImpressora) && !ImpressorasDisponiveis.Contains(SelectedImpressora))
        {
            ImpressorasDisponiveis.Insert(0, SelectedImpressora);
        }
    }

    public void Salvar()
    {
        var config = new ConfiguracaoImpressora
        {
            NomeImpressora = SelectedImpressora?.Trim() ?? string.Empty,
            LarguraColunas = LarguraColunas,
            ModoImpressao = ModoImpressao,
            CortarPapel = CortarPapel,
            ImprimirNegrito = ImprimirNegrito,
            DuasVias = DuasVias,
            MensagemRodape = MensagemRodape?.Trim() ?? string.Empty
        };

        _configService.SalvarConfiguracao(config);
        ExibirMensagem("Configurações da impressora salvas com sucesso!", true);
        IsOpen = false;
        OnFechado?.Invoke();
    }

    public async Task ImprimirTesteAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedImpressora))
        {
            ExibirMensagem("Selecione uma impressora instalada para testar.", false);
            return;
        }

        try
        {
            IsBusy = true;
            ExibirMensagem("Enviando cupom de teste para a impressora...", true);

            var empresa = await _configService.ObterDadosEmpresaAsync();
            var config = new ConfiguracaoImpressora
            {
                NomeImpressora = SelectedImpressora,
                LarguraColunas = LarguraColunas,
                CortarPapel = CortarPapel,
                ImprimirNegrito = ImprimirNegrito,
                MensagemRodape = MensagemRodape
            };

            int col = config.LarguraColunas > 0 ? config.LarguraColunas : 42;
            string linha = new('=', col);
            string div = new('-', col);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(linha);
            sb.AppendLine(Centralizar(empresa.NomeFantasia?.ToUpperInvariant() ?? "COMPUSERVE SISTEMAS", col));
            sb.AppendLine(Centralizar("TESTE DE IMPRESSAO TERMICA", col));
            sb.AppendLine(div);
            sb.AppendLine($"DATA: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"IMPRESSORA: {SelectedImpressora}");
            sb.AppendLine($"LARGURA BOBINA: {(config.IsBobina80mm ? $"80mm ({col} colunas)" : "58mm (32 colunas)")}");
            sb.AppendLine($"GUILHOTINA: {(CortarPapel ? "HABILITADA" : "DESABILITADA")}");
            sb.AppendLine($"IMPRESSAO NEGRITO: {(ImprimirNegrito ? "ATIVADA (MAIS FORTE)" : "DESATIVADA")}");
            sb.AppendLine(div);
            sb.AppendLine("123456789012345678901234567890123456789012345678".Substring(0, col));
            sb.AppendLine(div);
            sb.AppendLine(Centralizar("SERVSIC PV - IMPRESSAO OK!", col));
            sb.AppendLine(linha);
            sb.AppendLine(new string(' ', col));
            sb.AppendLine(new string(' ', col));

            var resultado = await Task.Run(() => _impressaoService.ImprimirTexto(SelectedImpressora, sb.ToString(), CortarPapel, ImprimirNegrito));

            if (resultado.Sucesso)
            {
                ExibirMensagem("Cupom de teste impresso com sucesso!", true);
            }
            else
            {
                ExibirMensagem($"Erro: {resultado.Mensagem}", false);
            }
        }
        catch (Exception ex)
        {
            ExibirMensagem($"Erro no envio: {ex.Message}", false);
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

    private void ExibirMensagem(string msg, bool sucesso)
    {
        StatusMensagem = msg;
        IsSucesso = sucesso;
    }

    private static string Centralizar(string texto, int largura)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        if (texto.Length >= largura) return texto.Substring(0, largura);
        int espacos = (largura - texto.Length) / 2;
        return texto.PadLeft(texto.Length + espacos).PadRight(largura);
    }
}
