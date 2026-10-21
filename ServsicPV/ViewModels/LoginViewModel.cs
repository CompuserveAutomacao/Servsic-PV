using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ServsicPV.Common;
using ServSic.Core.Config;
using ServSic.Core.Data;
using ServSic.Core.Models;
using ServSic.Core.Services;

namespace ServsicPV.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly UserPreferencesService _userPreferencesService = new();
    private Usuario? _selectedUser;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _isConnected;
    private string _connectionStatus = "Verificando Servcom.dll...";
    private string _databaseInfo = string.Empty;

    public ObservableCollection<Usuario> ActiveUsers { get; } = new();
    public AtualizacaoViewModel AtualizacaoViewModel { get; } = new();

    public Usuario? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                ErrorMessage = string.Empty;
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ErrorMessage = string.Empty;
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public string DatabaseInfo
    {
        get => _databaseInfo;
        set => SetProperty(ref _databaseInfo, value);
    }

    // Propriedades da Janela Modal de Configuração do Banco
    private bool _isConfigOpen;
    private string _configFilePath = string.Empty;
    private string _configServer = @".\qg";
    private string _configDatabase = "prod silcon";
    private string _configUser = "sa";
    private string _configPassword = string.Empty;
    private string _configStatusMessage = string.Empty;
    private bool _isConfigSuccess;
    private bool _isTestingConfig;

    public bool IsConfigOpen
    {
        get => _isConfigOpen;
        set => SetProperty(ref _isConfigOpen, value);
    }

    public string ConfigFilePath
    {
        get => _configFilePath;
        set => SetProperty(ref _configFilePath, value);
    }

    public string ConfigServer
    {
        get => _configServer;
        set => SetProperty(ref _configServer, value);
    }

    public string ConfigDatabase
    {
        get => _configDatabase;
        set => SetProperty(ref _configDatabase, value);
    }

    public string ConfigUser
    {
        get => _configUser;
        set => SetProperty(ref _configUser, value);
    }

    public string ConfigPassword
    {
        get => _configPassword;
        set => SetProperty(ref _configPassword, value);
    }

    public string ConfigStatusMessage
    {
        get => _configStatusMessage;
        set => SetProperty(ref _configStatusMessage, value);
    }

    public bool IsConfigSuccess
    {
        get => _isConfigSuccess;
        set => SetProperty(ref _isConfigSuccess, value);
    }

    public bool IsTestingConfig
    {
        get => _isTestingConfig;
        set => SetProperty(ref _isTestingConfig, value);
    }

    public Action<Usuario>? OnLoginSuccess { get; set; }

    public ICommand LoginCommand { get; }
    public ICommand RefreshUsersCommand { get; }
    public ICommand OpenConfigCommand { get; }
    public ICommand CloseConfigCommand { get; }
    public ICommand TestarConexaoConfigCommand { get; }
    public ICommand SalvarConexaoConfigCommand { get; }

    public LoginViewModel(AuthService? authService = null)
    {
        _authService = authService ?? new AuthService();

        LoginCommand = new RelayCommand(async _ => await ExecuteLoginAsync(), _ => CanExecuteLogin());
        RefreshUsersCommand = new RelayCommand(async _ => await CarregarUsuariosAsync());
        OpenConfigCommand = new RelayCommand(_ => AbrirConfiguracao());
        CloseConfigCommand = new RelayCommand(_ => FecharConfiguracao());
        TestarConexaoConfigCommand = new RelayCommand(async _ => await TestarConexaoConfigAsync());
        SalvarConexaoConfigCommand = new RelayCommand(async _ => await SalvarConexaoConfigAsync());

        _ = InicializarConexaoEUsuariosAsync();
    }

    private bool CanExecuteLogin()
    {
        return !IsBusy && SelectedUser != null;
    }

    public void AbrirConfiguracao()
    {
        var cfg = DbConnectionFactory.GetConfig();
        ConfigFilePath = string.IsNullOrWhiteSpace(cfg.FilePath) ? "Servcom.dll" : cfg.FilePath;
        ConfigServer = cfg.Server;
        ConfigDatabase = cfg.Database;
        ConfigUser = cfg.UserId;
        ConfigPassword = cfg.Password;
        ConfigStatusMessage = string.Empty;
        IsConfigOpen = true;
    }

    public void FecharConfiguracao()
    {
        IsConfigOpen = false;
        ConfigStatusMessage = string.Empty;
    }

    public async Task TestarConexaoConfigAsync()
    {
        try
        {
            IsTestingConfig = true;
            ConfigStatusMessage = "Testando conexão com o SQL Server...";
            IsConfigSuccess = false;

            var testConfig = new ServcomConfig
            {
                Server = ConfigServer?.Trim() ?? string.Empty,
                Database = ConfigDatabase?.Trim() ?? string.Empty,
                UserId = ConfigUser?.Trim() ?? string.Empty,
                Password = ConfigPassword ?? string.Empty
            };

            var (success, error) = await ServcomReader.TestConnectionAsync(testConfig);
            if (success)
            {
                IsConfigSuccess = true;
                ConfigStatusMessage = "Sucesso: Conexão com o banco estabelecida com êxito!";
            }
            else
            {
                IsConfigSuccess = false;
                ConfigStatusMessage = $"Falha na conexão: {error}";
            }
        }
        catch (Exception ex)
        {
            IsConfigSuccess = false;
            ConfigStatusMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsTestingConfig = false;
        }
    }

    public async Task SalvarConexaoConfigAsync()
    {
        try
        {
            IsBusy = true;
            ConfigStatusMessage = "Gravando Servcom.dll e atualizando conexão...";

            var newConfig = new ServcomConfig
            {
                Server = ConfigServer?.Trim() ?? string.Empty,
                Database = ConfigDatabase?.Trim() ?? string.Empty,
                UserId = ConfigUser?.Trim() ?? string.Empty,
                Password = ConfigPassword ?? string.Empty
            };

            DbConnectionFactory.SaveAndApplyConfig(newConfig);
            ConfigFilePath = newConfig.FilePath;

            ConfigStatusMessage = "Servcom.dll gravado com sucesso! Conectando...";
            IsConfigSuccess = true;

            await Task.Delay(400);
            FecharConfiguracao();

            await InicializarConexaoEUsuariosAsync();
        }
        catch (Exception ex)
        {
            IsConfigSuccess = false;
            ConfigStatusMessage = $"Erro ao salvar Servcom.dll: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InicializarConexaoEUsuariosAsync()
    {
        try
        {
            IsBusy = true;
            ConnectionStatus = "Verificando Servcom.dll...";

            var config = DbConnectionFactory.GetConfig();
            DatabaseInfo = $"Servidor: {config.Server} | Base: {config.Database}";

            // Dispara verificação de novas versões em segundo plano
            _ = AtualizacaoViewModel.VerificarSilenciosoAsync();

            bool ok = await _authService.TestarConexaoAsync();
            IsConnected = ok;

            if (ok)
            {
                ConnectionStatus = $"Conectado: {config.Server} ({config.Database})";
                ErrorMessage = string.Empty;
                await DatabaseMigrationService.ExecutarMigracaoAsync(forcar: true);
                await CarregarUsuariosAsync();
            }
            else
            {
                ConnectionStatus = "Não conectado (Verifique Servcom.dll)";
                ErrorMessage = "Não foi possível conectar ao banco de dados. Clique no ícone de engrenagem ⚙ no canto superior para configurar a conexão.";
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatus = "Erro ao ler Servcom.dll";
            ErrorMessage = $"Erro na conexão: {ex.Message}. Clique na engrenagem ⚙ para configurar.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CarregarUsuariosAsync()
    {
        try
        {
            ActiveUsers.Clear();
            var usuarios = await _authService.ListarUsuariosAtivosAsync();
            foreach (var u in usuarios)
            {
                ActiveUsers.Add(u);
            }

            if (ActiveUsers.Count > 0)
            {
                var ultimoUsuario = _userPreferencesService.ObterUltimoUsuario();
                Usuario? usuarioParaSelecionar = null;

                if (!string.IsNullOrWhiteSpace(ultimoUsuario))
                {
                    usuarioParaSelecionar = System.Linq.Enumerable.FirstOrDefault(ActiveUsers, u =>
                        string.Equals(u.Nome?.Trim(), ultimoUsuario.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                SelectedUser = usuarioParaSelecionar ?? ActiveUsers[0];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao carregar usuários: {ex.Message}";
        }
    }

    private async Task ExecuteLoginAsync()
    {
        if (SelectedUser == null) return;

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var usuario = await _authService.AutenticarAsync(SelectedUser.Nome, Password);
            if (usuario != null)
            {
                // Guarda o último usuário logado com sucesso
                _userPreferencesService.SalvarUltimoUsuario(usuario.Nome);

                ConnectionStatus = "Sincronizando banco de dados...";
                await DatabaseMigrationService.ExecutarMigracaoAsync(forcar: true);
                OnLoginSuccess?.Invoke(usuario);
            }
            else
            {
                ErrorMessage = "Senha incorreta! Verifique e tente novamente.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro na autenticação: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
