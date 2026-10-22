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
    private SolicitacaoLiberacao? _solicitacaoPendenteSelecionada;
    private SolicitacaoLiberacao? _solicitacaoHistoricoSelecionada;
    private bool _isAbaPendentes = true;
    private bool _isBusy;
    private int _contadorPendentes;
    private bool _isRecusaDialogOpen;
    private string _motivoRecusa = string.Empty;
    private string _mensagemAviso = string.Empty;
    private bool _isMensagemSucesso;
    private bool _isMensagemVisivel;
    private readonly SemaphoreSlim _carregamentoSemaphore = new(1, 1);

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

    public SolicitacaoLiberacao? SolicitacaoPendenteSelecionada
    {
        get => _solicitacaoPendenteSelecionada;
        set
        {
            if (SetProperty(ref _solicitacaoPendenteSelecionada, value))
            {
                if (IsAbaPendentes)
                {
                    OnPropertyChanged(nameof(SolicitacaoSelecionada));
                    OnPropertyChanged(nameof(PodeExecutarAcao));
                }
                (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public SolicitacaoLiberacao? SolicitacaoHistoricoSelecionada
    {
        get => _solicitacaoHistoricoSelecionada;
        set
        {
            if (SetProperty(ref _solicitacaoHistoricoSelecionada, value))
            {
                if (!IsAbaPendentes)
                {
                    OnPropertyChanged(nameof(SolicitacaoSelecionada));
                    OnPropertyChanged(nameof(PodeExecutarAcao));
                }
                (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public SolicitacaoLiberacao? SolicitacaoSelecionada => IsAbaPendentes ? SolicitacaoPendenteSelecionada : SolicitacaoHistoricoSelecionada;

    public bool IsAbaPendentes
    {
        get => _isAbaPendentes;
        set
        {
            if (SetProperty(ref _isAbaPendentes, value))
            {
                OnPropertyChanged(nameof(IsAbaHistorico));
                OnPropertyChanged(nameof(SolicitacaoSelecionada));
                OnPropertyChanged(nameof(PodeExecutarAcao));
                (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsAbaHistorico => !IsAbaPendentes;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(PodeExecutarAcao));
                (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
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

    public bool PodeExecutarAcao => IsAbaPendentes && SolicitacaoPendenteSelecionada != null && SolicitacaoPendenteSelecionada.IsPendente && !IsBusy;

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
        OnPropertyChanged(nameof(SolicitacaoSelecionada));
        OnPropertyChanged(nameof(PodeExecutarAcao));
        (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
        if (!await _carregamentoSemaphore.WaitAsync(0))
        {
            // Já existe um carregamento em andamento, evita concorrência e recriação desordenada
            return;
        }

        try
        {
            var pendentes = await _liberacaoService.ListarPendentesAsync();
            var historico = await _liberacaoService.ListarHistoricoHojeAsync();

            var novasPendentes = new List<SolicitacaoLiberacao>();
            foreach (var p in pendentes)
            {
                if (!_idsConhecidos.Contains(p.Controle))
                {
                    _idsConhecidos.Add(p.Controle);
                    if (_primeiraCargaRealizada)
                    {
                        novasPendentes.Add(p);
                    }
                }
            }

            if (!_primeiraCargaRealizada)
            {
                _primeiraCargaRealizada = true;
            }
            else if (novasPendentes.Count > 0 && tocarSomSeHouverNovo)
            {
                TocarAlertaSonoro();
                foreach (var nova in novasPendentes)
                {
                    try
                    {
                        NotificationService.NotificarNovaSolicitacao(nova);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LiberacoesViewModel] Falha ao notificar Windows: {ex.Message}");
                    }
                }
            }

            var dispatcher = _dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            dispatcher?.TryEnqueue(() =>
            {
                try
                {
                    SincronizarColecao(SolicitacoesPendentes, pendentes);
                    SincronizarColecao(HistoricoHoje, historico);

                    ContadorPendentes = SolicitacoesPendentes.Count;

                    // Preserva a seleção se ainda existir, ou seleciona o primeiro
                    if (SolicitacaoPendenteSelecionada == null || !SolicitacoesPendentes.Any(x => x.Controle == SolicitacaoPendenteSelecionada.Controle))
                    {
                        SolicitacaoPendenteSelecionada = SolicitacoesPendentes.FirstOrDefault();
                    }

                    if (SolicitacaoHistoricoSelecionada == null || !HistoricoHoje.Any(x => x.Controle == SolicitacaoHistoricoSelecionada.Controle))
                    {
                        SolicitacaoHistoricoSelecionada = HistoricoHoje.FirstOrDefault();
                    }

                    OnPropertyChanged(nameof(SolicitacaoSelecionada));
                    OnPropertyChanged(nameof(PodeExecutarAcao));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LiberacoesViewModel] Erro na sincronização da UI: {ex.Message}");
                }
            });
        }
        finally
        {
            _carregamentoSemaphore.Release();
        }
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

        // 2. Inserir itens novos ou atualizar existentes sem destruir a coleção e NUNCA usar Move
        for (int i = 0; i < novos.Count; i++)
        {
            var novo = novos[i];
            var existente = destino.FirstOrDefault(d => d.Controle == novo.Controle);

            if (existente == null)
            {
                if (i <= destino.Count)
                    destino.Insert(i, novo);
                else
                    destino.Add(novo);
            }
            else
            {
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
        if (IsBusy) return;

        var sol = SolicitacaoPendenteSelecionada;
        if (sol == null || !sol.IsPendente)
            return;

        int lkSup = UsuarioLogado?.Controle ?? 0;
        string nomeSup = UsuarioLogado?.Nome ?? "Supervisor";
        int controle = sol.Controle;

        try
        {
            IsBusy = true;
            (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();

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
            (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public void AbrirDialogRecusa()
    {
        var sol = SolicitacaoPendenteSelecionada;
        if (sol == null || !sol.IsPendente || IsBusy)
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
        if (IsBusy) return;

        var sol = SolicitacaoPendenteSelecionada;
        if (sol == null || !sol.IsPendente)
            return;

        int lkSup = UsuarioLogado?.Controle ?? 0;
        string nomeSup = UsuarioLogado?.Nome ?? "Supervisor";
        int controle = sol.Controle;
        string motivo = string.IsNullOrWhiteSpace(MotivoRecusa) ? "Recusado pelo supervisor" : MotivoRecusa.Trim();

        try
        {
            IsBusy = true;
            IsRecusaDialogOpen = false;
            (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();

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
            (AprovarCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AbrirRecusaCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
