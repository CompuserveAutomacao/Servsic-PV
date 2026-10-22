using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using ServsicPV.Common;
using ServSic.Core.Models;
using ServSic.Core.Services;

namespace ServsicPV.ViewModels;

public class LiberacoesViewModel : ViewModelBase
{
    private readonly LiberacaoService _liberacaoService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly HashSet<int> _idsConhecidos = new();
    private bool _primeiraCargaRealizada;
    private CancellationTokenSource? _monitoramentoCts;

    private Usuario? _usuarioLogado;
    private SolicitacaoLiberacao? _solicitacaoSelecionada;
    private bool _isAbaPendentes = true;
    private bool _isBusy;
    private int _contadorPendentes;
    private bool _isRecusaDialogOpen;
    private string _motivoRecusa = string.Empty;
    private string _mensagemAviso = string.Empty;
    private bool _isMensagemSucesso;
    private bool _isMensagemVisivel;

    public Action? OnVoltarAoPedido;
    public Action? OnSolicitarFocoGrade;

    public LiberacoesViewModel(LiberacaoService? liberacaoService = null)
    {
        _liberacaoService = liberacaoService ?? new LiberacaoService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        AprovarCommand = new RelayCommand(async _ => await AprovarAsync(), _ => PodeExecutarAcao);
        AbrirRecusaCommand = new RelayCommand(_ => AbrirDialogRecusa(), _ => PodeExecutarAcao);
        ConfirmarRecusaCommand = new RelayCommand(async _ => await ConfirmarRecusaAsync());
        CancelarRecusaCommand = new RelayCommand(_ => CancelarDialogRecusa());
        AtualizarCommand = new RelayCommand(async _ => await CarregarDadosAsync(tocarSomSeHouverNovo: false));
        AlternarParaPendentesCommand = new RelayCommand(_ => SetAbaPendentes(true));
        AlternarParaHistoricoCommand = new RelayCommand(_ => SetAbaPendentes(false));
        VoltarCommand = new RelayCommand(_ => OnVoltarAoPedido?.Invoke());
    }

    public ObservableCollection<SolicitacaoLiberacao> SolicitacoesPendentes { get; } = new();
    public ObservableCollection<SolicitacaoLiberacao> HistoricoHoje { get; } = new();

    public Usuario? UsuarioLogado
    {
        get => _usuarioLogado;
        set => SetProperty(ref _usuarioLogado, value);
    }

    public SolicitacaoLiberacao? SolicitacaoSelecionada
    {
        get => _solicitacaoSelecionada;
        set
        {
            if (SetProperty(ref _solicitacaoSelecionada, value))
            {
                (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsAbaPendentes
    {
        get => _isAbaPendentes;
        set
        {
            if (SetProperty(ref _isAbaPendentes, value))
            {
                OnPropertyChanged(nameof(IsAbaHistorico));
            }
        }
    }

    public bool IsAbaHistorico => !IsAbaPendentes;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public int ContadorPendentes
    {
        get => _contadorPendentes;
        set => SetProperty(ref _contadorPendentes, value);
    }

    public bool IsRecusaDialogOpen
    {
        get => _isRecusaDialogOpen;
        set => SetProperty(ref _isRecusaDialogOpen, value);
    }

    public string MotivoRecusa
    {
        get => _motivoRecusa;
        set => SetProperty(ref _motivoRecusa, value);
    }

    public string MensagemAviso
    {
        get => _mensagemAviso;
        set => SetProperty(ref _mensagemAviso, value);
    }

    public bool IsMensagemSucesso
    {
        get => _isMensagemSucesso;
        set => SetProperty(ref _isMensagemSucesso, value);
    }

    public bool IsMensagemVisivel
    {
        get => _isMensagemVisivel;
        set => SetProperty(ref _isMensagemVisivel, value);
    }

    public bool PodeExecutarAcao => SolicitacaoSelecionada != null && SolicitacaoSelecionada.IsPendente && !IsBusy;

    public ICommand AprovarCommand { get; }
    public ICommand AbrirRecusaCommand { get; }
    public ICommand ConfirmarRecusaCommand { get; }
    public ICommand CancelarRecusaCommand { get; }
    public ICommand AtualizarCommand { get; }
    public ICommand AlternarParaPendentesCommand { get; }
    public ICommand AlternarParaHistoricoCommand { get; }
    public ICommand VoltarCommand { get; }

    public void SetAbaPendentes(bool pendentes)
    {
        if (IsAbaPendentes == pendentes) return;
        IsAbaPendentes = pendentes;
        SolicitacaoSelecionada = pendentes ? SolicitacoesPendentes.FirstOrDefault() : HistoricoHoje.FirstOrDefault();
    }

    public void IniciarMonitoramento()
    {
        PararMonitoramento();
        _monitoramentoCts = new CancellationTokenSource();
        var token = _monitoramentoCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await CarregarDadosSilenciosoAsync();
                }
                catch
                {
                    // Ignora falhas temporárias de rede no polling
                }

                try
                {
                    await Task.Delay(2500, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    public void PararMonitoramento()
    {
        _monitoramentoCts?.Cancel();
        _monitoramentoCts?.Dispose();
        _monitoramentoCts = null;
    }

    public async Task CarregarDadosAsync(bool tocarSomSeHouverNovo = true)
    {
        try
        {
            IsBusy = true;
            await ExecutarCarregamentoAsync(tocarSomSeHouverNovo);
        }
        catch (Exception ex)
        {
            ExibirMensagem($"Erro ao carregar solicitações: {ex.Message}", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CarregarDadosSilenciosoAsync()
    {
        await ExecutarCarregamentoAsync(tocarSomSeHouverNovo: true);
    }

    private async Task ExecutarCarregamentoAsync(bool tocarSomSeHouverNovo)
    {
        var pendentes = await _liberacaoService.ListarPendentesAsync();
        var historico = await _liberacaoService.ListarHistoricoHojeAsync();

        bool temNovo = false;
        foreach (var p in pendentes)
        {
            if (!_idsConhecidos.Contains(p.Controle))
            {
                _idsConhecidos.Add(p.Controle);
                if (_primeiraCargaRealizada)
                {
                    temNovo = true;
                }
            }
        }

        if (!_primeiraCargaRealizada)
        {
            _primeiraCargaRealizada = true;
        }
        else if (temNovo && tocarSomSeHouverNovo)
        {
            TocarAlertaSonoro();
        }

        var dispatcher = _dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        dispatcher?.TryEnqueue(() =>
        {
            SincronizarColecao(SolicitacoesPendentes, pendentes);
            SincronizarColecao(HistoricoHoje, historico);

            ContadorPendentes = SolicitacoesPendentes.Count;

            // Preserva a seleção se ainda existir, ou seleciona o primeiro
            if (IsAbaPendentes)
            {
                if (SolicitacaoSelecionada == null || !SolicitacoesPendentes.Any(x => x.Controle == SolicitacaoSelecionada.Controle))
                {
                    SolicitacaoSelecionada = SolicitacoesPendentes.FirstOrDefault();
                }
            }
            else
            {
                if (SolicitacaoSelecionada == null || !HistoricoHoje.Any(x => x.Controle == SolicitacaoSelecionada.Controle))
                {
                    SolicitacaoSelecionada = HistoricoHoje.FirstOrDefault();
                }
            }
        });
    }

    private void SincronizarColecao(ObservableCollection<SolicitacaoLiberacao> destino, List<SolicitacaoLiberacao> novos)
    {
        // 1. Remover itens que saíram
        for (int i = destino.Count - 1; i >= 0; i--)
        {
            if (!novos.Any(n => n.Controle == destino[i].Controle))
            {
                destino.RemoveAt(i);
            }
        }

        // 2. Inserir ou atualizar na posição correta sem destruir a coleção
        for (int i = 0; i < novos.Count; i++)
        {
            var novo = novos[i];
            var existente = destino.FirstOrDefault(d => d.Controle == novo.Controle);

            if (existente == null)
            {
                if (i < destino.Count)
                    destino.Insert(i, novo);
                else
                    destino.Add(novo);
            }
            else
            {
                int indexAtual = destino.IndexOf(existente);
                if (indexAtual != i && indexAtual >= 0 && i < destino.Count)
                {
                    destino.Move(indexAtual, i);
                }

                // Atualizar dados de status caso tenham mudado
                if (existente.Status != novo.Status)
                    existente.Status = novo.Status;
                if (existente.MotivoRecusa != novo.MotivoRecusa)
                    existente.MotivoRecusa = novo.MotivoRecusa;
                if (existente.NomeSupervisor != novo.NomeSupervisor)
                    existente.NomeSupervisor = novo.NomeSupervisor;
                if (existente.DataHoraResposta != novo.DataHoraResposta)
                    existente.DataHoraResposta = novo.DataHoraResposta;
            }
        }
    }

    public async Task AprovarAsync()
    {
        if (SolicitacaoSelecionada == null || !SolicitacaoSelecionada.IsPendente)
            return;

        int lkSup = UsuarioLogado?.Controle ?? 0;
        string nomeSup = UsuarioLogado?.Nome ?? "Supervisor";
        int controle = SolicitacaoSelecionada.Controle;

        try
        {
            IsBusy = true;
            bool ok = await _liberacaoService.AprovarSolicitacaoAsync(controle, lkSup, nomeSup);
            if (ok)
            {
                ExibirMensagem($"Solicitação #{controle} liberada com sucesso por {nomeSup}!", true);
                await CarregarDadosAsync(tocarSomSeHouverNovo: false);
            }
            else
            {
                ExibirMensagem("Não foi possível aprovar a solicitação. Ela pode já ter sido alterada.", false);
            }
        }
        catch (Exception ex)
        {
            ExibirMensagem($"Erro ao aprovar: {ex.Message}", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void AbrirDialogRecusa()
    {
        if (SolicitacaoSelecionada == null || !SolicitacaoSelecionada.IsPendente)
            return;

        MotivoRecusa = string.Empty;
        IsRecusaDialogOpen = true;
    }

    public void CancelarDialogRecusa()
    {
        IsRecusaDialogOpen = false;
        MotivoRecusa = string.Empty;
    }

    public async Task ConfirmarRecusaAsync()
    {
        if (SolicitacaoSelecionada == null || !SolicitacaoSelecionada.IsPendente)
            return;

        int lkSup = UsuarioLogado?.Controle ?? 0;
        string nomeSup = UsuarioLogado?.Nome ?? "Supervisor";
        int controle = SolicitacaoSelecionada.Controle;
        string motivo = string.IsNullOrWhiteSpace(MotivoRecusa) ? "Recusado pelo supervisor" : MotivoRecusa.Trim();

        try
        {
            IsBusy = true;
            IsRecusaDialogOpen = false;

            bool ok = await _liberacaoService.RecusarSolicitacaoAsync(controle, lkSup, nomeSup, motivo);
            if (ok)
            {
                ExibirMensagem($"Solicitação #{controle} recusada com sucesso.", true);
                await CarregarDadosAsync(tocarSomSeHouverNovo: false);
            }
            else
            {
                ExibirMensagem("Não foi possível recusar a solicitação.", false);
            }
        }
        catch (Exception ex)
        {
            ExibirMensagem($"Erro ao recusar: {ex.Message}", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void TocarAlertaSonoro()
    {
        _ = Task.Run(() =>
        {
            try
            {
                Console.Beep(920, 180);
                Thread.Sleep(80);
                Console.Beep(1150, 220);
            }
            catch
            {
                // Silencioso se dispositivo não possuir saída de beep
            }
        });
    }

    private void ExibirMensagem(string mensagem, bool sucesso, int tempoMs = 4000)
    {
        MensagemAviso = mensagem;
        IsMensagemSucesso = sucesso;
        IsMensagemVisivel = true;

        _ = Task.Run(async () =>
        {
            await Task.Delay(tempoMs);
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (MensagemAviso == mensagem)
                {
                    IsMensagemVisivel = false;
                }
            });
        });
    }
}
