using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ServsicPV.Common;
using ServSic.Core.Models;
using ServSic.Core.Services;

namespace ServsicPV.ViewModels;

public class DesempenhoViewModel : ViewModelBase
{
    private readonly DesempenhoService _desempenhoService;
    private Usuario? _usuarioLogado;
    private bool _isCarregando;
    private string _mensagemStatus = string.Empty;
    private DesempenhoVendedorModel _dados = new();

    public DesempenhoViewModel(DesempenhoService desempenhoService)
    {
        _desempenhoService = desempenhoService;
        AtualizarCommand = new RelayCommand(async () => await CarregarDadosAsync());
        VoltarCommand = new RelayCommand(() => OnVoltarAoBalcao?.Invoke());
    }

    public Usuario? UsuarioLogado
    {
        get => _usuarioLogado;
        set
        {
            if (SetProperty(ref _usuarioLogado, value))
            {
                OnPropertyChanged(nameof(NomeOperadorExibicao));
                OnPropertyChanged(nameof(NomeVendedorExibicao));
            }
        }
    }

    public bool IsCarregando
    {
        get => _isCarregando;
        set => SetProperty(ref _isCarregando, value);
    }

    public string MensagemStatus
    {
        get => _mensagemStatus;
        set => SetProperty(ref _mensagemStatus, value);
    }

    public DesempenhoVendedorModel Dados
    {
        get => _dados;
        private set
        {
            if (SetProperty(ref _dados, value))
            {
                OnPropertyChanged(nameof(PeriodoDescricao));
                OnPropertyChanged(nameof(TotalFaturadoFormatado));
                OnPropertyChanged(nameof(TotalGeralFormatado));
                OnPropertyChanged(nameof(TicketMedioVendasFormatado));
                OnPropertyChanged(nameof(TicketMedioGeralFormatado));
                OnPropertyChanged(nameof(TaxaConversaoFormatada));
                OnPropertyChanged(nameof(QtdVendasDescricao));
                OnPropertyChanged(nameof(TotalVendasFormatado));
                OnPropertyChanged(nameof(QtdPreVendasDescricao));
                OnPropertyChanged(nameof(TotalPreVendasFormatado));
                OnPropertyChanged(nameof(QtdOrcamentosDescricao));
                OnPropertyChanged(nameof(TotalOrcamentosFormatado));
                OnPropertyChanged(nameof(QtdCanceladasDescricao));
                OnPropertyChanged(nameof(TotalCanceladasFormatado));
                OnPropertyChanged(nameof(TotalAtendimentosDescricao));
                OnPropertyChanged(nameof(PercentualVendas));
                OnPropertyChanged(nameof(PercentualPreVendas));
                OnPropertyChanged(nameof(PercentualOrcamentos));
                OnPropertyChanged(nameof(PercentualVendasFormatado));
                OnPropertyChanged(nameof(PercentualPreVendasFormatado));
                OnPropertyChanged(nameof(PercentualOrcamentosFormatado));
                OnPropertyChanged(nameof(TemDados));
                OnPropertyChanged(nameof(TemTopProdutos));
                OnPropertyChanged(nameof(Dias));
                OnPropertyChanged(nameof(TopProdutos));
                OnPropertyChanged(nameof(Top3Vendedores));
                OnPropertyChanged(nameof(MensagemPosicaoUsuario));
                OnPropertyChanged(nameof(TemTop3));
            }
        }
    }

    public string NomeOperadorExibicao => UsuarioLogado?.Nome ?? "Operador";
    public string NomeVendedorExibicao => !string.IsNullOrWhiteSpace(UsuarioLogado?.NomeVendedor)
        ? UsuarioLogado.NomeVendedor
        : (UsuarioLogado?.LkVendedor.HasValue == true ? $"Vendedor #{UsuarioLogado.LkVendedor}" : NomeOperadorExibicao);

    public string PeriodoDescricao => Dados != null && Dados.DataInicio != default
        ? $"Últimos 7 dias • {Dados.DataInicio:dd/MM} a {Dados.DataFim:dd/MM}"
        : "Últimos 7 dias";

    public string TotalFaturadoFormatado => $"R$ {Dados.TotalFaturado:N2}";
    public string TotalGeralFormatado => $"R$ {Dados.TotalGeral:N2}";
    public string TicketMedioVendasFormatado => $"R$ {Dados.TicketMedioVendas:N2}";
    public string TicketMedioGeralFormatado => $"R$ {Dados.TicketMedioGeral:N2}";
    public string TaxaConversaoFormatada => $"{Dados.TaxaConversao:N1}%";

    public string QtdVendasDescricao => $"{Dados.QtdVendas} {(Dados.QtdVendas == 1 ? "venda" : "vendas")}";
    public string TotalVendasFormatado => $"R$ {Dados.TotalVendas:N2}";

    public string QtdPreVendasDescricao => $"{Dados.QtdPreVendas} {(Dados.QtdPreVendas == 1 ? "pré-venda" : "pré-vendas")}";
    public string TotalPreVendasFormatado => $"R$ {Dados.TotalPreVendas:N2}";

    public string QtdOrcamentosDescricao => $"{Dados.QtdOrcamentos} {(Dados.QtdOrcamentos == 1 ? "orçamento" : "orçamentos")}";
    public string TotalOrcamentosFormatado => $"R$ {Dados.TotalOrcamentos:N2}";

    public string QtdCanceladasDescricao => $"{Dados.QtdCanceladas} {(Dados.QtdCanceladas == 1 ? "cancelado" : "cancelados")}";
    public string TotalCanceladasFormatado => $"R$ {Dados.TotalCanceladas:N2}";

    public string TotalAtendimentosDescricao => $"{Dados.TotalAtendimentos} {(Dados.TotalAtendimentos == 1 ? "atendimento" : "atendimentos")}";

    public double PercentualVendas => Dados.PercentualVendas;
    public double PercentualPreVendas => Dados.PercentualPreVendas;
    public double PercentualOrcamentos => Dados.PercentualOrcamentos;

    public string PercentualVendasFormatado => $"{Dados.PercentualVendas:N1}%";
    public string PercentualPreVendasFormatado => $"{Dados.PercentualPreVendas:N1}%";
    public string PercentualOrcamentosFormatado => $"{Dados.PercentualOrcamentos:N1}%";
    public string TicketMedioGeralSubtitulo => $"Geral: {TicketMedioGeralFormatado}";

    public bool TemDados => Dados.TotalAtendimentos > 0;
    public bool TemTopProdutos => Dados.TopProdutos.Count > 0;
    public bool TemTop3 => Dados.Top3Vendedores.Count > 0;

    public System.Collections.Generic.List<DesempenhoDiarioModel> Dias => Dados.Dias;
    public System.Collections.Generic.List<DesempenhoProdutoModel> TopProdutos => Dados.TopProdutos;
    public System.Collections.Generic.List<RankingVendedorModel> Top3Vendedores => Dados.Top3Vendedores;
    public string MensagemPosicaoUsuario => Dados.MensagemPosicaoUsuario;

    public Action? OnVoltarAoBalcao { get; set; }
    public ICommand AtualizarCommand { get; }
    public ICommand VoltarCommand { get; }

    public async Task CarregarDadosAsync()
    {
        if (IsCarregando) return;

        IsCarregando = true;
        MensagemStatus = "Atualizando indicadores dos últimos 7 dias...";

        try
        {
            var resultado = await _desempenhoService.ObterDesempenho7DiasAsync(
                UsuarioLogado?.Controle,
                UsuarioLogado?.LkVendedor,
                UsuarioLogado?.Nome,
                UsuarioLogado?.NomeVendedor);

            Dados = resultado;
            MensagemStatus = string.Empty;
        }
        catch (Exception ex)
        {
            MensagemStatus = $"Erro ao calcular indicadores: {ex.Message}";
        }
        finally
        {
            IsCarregando = false;
        }
    }
}
