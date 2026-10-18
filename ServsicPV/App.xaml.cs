using System;
using System.Globalization;
using System.IO;
using Microsoft.UI.Xaml;
using ServsicPV.Views;

namespace ServsicPV;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        };

        this.UnhandledException += (s, e) =>
        {
            LogCrash("Application.UnhandledException", e.Exception);
            e.Handled = true;
        };

        var ptBr = new CultureInfo("pt-BR");
        CultureInfo.CurrentCulture = ptBr;
        CultureInfo.CurrentUICulture = ptBr;
    }

    private static void LogCrash(string origem, Exception? ex)
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{origem}] {ex?.Message}\n{ex?.StackTrace}\nInner: {ex?.InnerException?.Message}\n\n");
        }
        catch { }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            _ = ServSic.Core.Data.DatabaseMigrationService.ExecutarMigracaoAsync();
            _window = new LoginView();
            _window.Activate();
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched", ex);
            throw;
        }
    }
}
