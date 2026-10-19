using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ServsicPV.Common;
using ServSic.Core.Services;

namespace ServsicPV.ViewModels;

public class AtualizacaoViewModel : ViewModelBase
{
    private bool _isOpen;
    private bool _isChecking;
    private bool _isDownloading;
    private string _currentVersion = UpdateService.ObterVersaoAtual();
    private string _newVersion = string.Empty;
    private string _changelog = string.Empty;
    private double _progress;
    private string _statusText = string.Empty;
    private string _errorMessage = string.Empty;
    private string _downloadUrl = string.Empty;

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public bool IsChecking
    {
        get => _isChecking;
        set => SetProperty(ref _isChecking, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(CanClose));
                (AtualizarCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (FecharCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanClose => !IsDownloading;

    public string CurrentVersion
    {
        get => _currentVersion;
        set => SetProperty(ref _currentVersion, value);
    }

    public string NewVersion
    {
        get => _newVersion;
        set => SetProperty(ref _newVersion, value);
    }

    public string Changelog
    {
        get => _changelog;
        set => SetProperty(ref _changelog, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand AtualizarCommand { get; }
    public ICommand FecharCommand { get; }
    public ICommand VerificarCommand { get; }

    public AtualizacaoViewModel()
    {
        AtualizarCommand = new RelayCommand(async _ => await ExecutarAtualizacaoAsync(), _ => !IsDownloading && !string.IsNullOrEmpty(_downloadUrl));
        FecharCommand = new RelayCommand(_ => Fechar(), _ => CanClose);
        VerificarCommand = new RelayCommand(async _ => await VerificarComFeedbackAsync());
    }

    public void Fechar()
    {
        if (IsDownloading) return;
        IsOpen = false;
        ErrorMessage = string.Empty;
    }

    public async Task<bool> VerificarSilenciosoAsync()
    {
        try
        {
            IsChecking = true;
            CurrentVersion = $"v{UpdateService.ObterVersaoAtual()}";
            var updateInfo = await UpdateService.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                CurrentVersion = updateInfo.CurrentVersion;
                NewVersion = updateInfo.NewVersion;
                Changelog = updateInfo.Changelog;
                _downloadUrl = updateInfo.DownloadUrl;
                ErrorMessage = string.Empty;
                StatusText = string.Empty;
                Progress = 0;
                IsOpen = true;
                return true;
            }
        }
        catch
        {
            // Silencia erro em verificação silenciosa
        }
        finally
        {
            IsChecking = false;
        }

        return false;
    }

    public async Task VerificarComFeedbackAsync(Action<string, bool>? notifyCallback = null)
    {
        try
        {
            IsChecking = true;
            CurrentVersion = $"v{UpdateService.ObterVersaoAtual()}";
            var updateInfo = await UpdateService.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                CurrentVersion = updateInfo.CurrentVersion;
                NewVersion = updateInfo.NewVersion;
                Changelog = updateInfo.Changelog;
                _downloadUrl = updateInfo.DownloadUrl;
                ErrorMessage = string.Empty;
                StatusText = string.Empty;
                Progress = 0;
                IsOpen = true;
            }
            else
            {
                notifyCallback?.Invoke($"O SERVSIC PV já está na versão mais recente ({CurrentVersion}).", true);
            }
        }
        catch (Exception ex)
        {
            notifyCallback?.Invoke($"Falha ao verificar atualizações: {ex.Message}", false);
        }
        finally
        {
            IsChecking = false;
        }
    }

    public async Task ExecutarAtualizacaoAsync()
    {
        if (string.IsNullOrEmpty(_downloadUrl) || IsDownloading) return;

        try
        {
            IsDownloading = true;
            ErrorMessage = string.Empty;
            Progress = 0;
            StatusText = "Iniciando download da atualização...";

            await UpdateService.ExecuteUpdateAsync(
                _downloadUrl,
                progress =>
                {
                    Progress = progress;
                },
                status =>
                {
                    StatusText = status;
                }
            );
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            ErrorMessage = $"Falha ao baixar atualização: {ex.Message}";
            StatusText = string.Empty;
        }
    }
}
