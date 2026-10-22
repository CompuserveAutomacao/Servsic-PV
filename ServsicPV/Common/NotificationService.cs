using System;
using System.Diagnostics;
using System.Security;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using ServSic.Core.Models;

namespace ServsicPV.Common;

public static class NotificationService
{
    private static bool _appNotificationRegistrado;
    public static Action? OnNotificacaoClicada;

    public static void Inicializar()
    {
        try
        {
            if (AppNotificationManager.IsSupported())
            {
                AppNotificationManager.Default.NotificationInvoked += (s, e) =>
                {
                    OnNotificacaoClicada?.Invoke();
                };
                AppNotificationManager.Default.Register();
                _appNotificationRegistrado = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationService] AppNotificationManager.Register fallback: {ex.Message}");
            _appNotificationRegistrado = false;
        }
    }

    public static void NotificarNovaSolicitacao(SolicitacaoLiberacao sol)
    {
        string titulo = "SERVSIC PV - Liberação Pendente";
        string operador = !string.IsNullOrWhiteSpace(sol.NomeUsuario) ? sol.NomeUsuario : "Operador";
        string terminal = !string.IsNullOrWhiteSpace(sol.Terminal) ? sol.Terminal : "Terminal";
        string tipo = sol.TipoDescricao;

        string linha1 = $"{tipo} - {operador} ({terminal})";
        string linha2 = !string.IsNullOrWhiteSpace(sol.Descricao)
            ? sol.Descricao
            : $"Solicitação #{sol.Controle} aguardando autorização do supervisor.";

        EnviarNotificacao(titulo, linha1, linha2);
    }

    public static void EnviarNotificacao(string titulo, string linha1, string? linha2 = null)
    {
        // 1. Tenta via AppNotificationManager (Windows App SDK)
        if (_appNotificationRegistrado)
        {
            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText(titulo)
                    .AddText(linha1);

                if (!string.IsNullOrWhiteSpace(linha2))
                {
                    builder.AddText(linha2);
                }

                var notif = builder.BuildNotification();
                AppNotificationManager.Default.Show(notif);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] Erro no AppNotificationManager.Show: {ex.Message}");
            }
        }

        // 2. Fallback via Windows.UI.Notifications.ToastNotificationManager com AUMID do Windows
        try
        {
            string escTitulo = SecurityElement.Escape(titulo) ?? titulo;
            string escLinha1 = SecurityElement.Escape(linha1) ?? linha1;
            string escLinha2 = SecurityElement.Escape(linha2 ?? string.Empty) ?? (linha2 ?? string.Empty);

            string xml = $@"<toast>
                <visual>
                    <binding template=""ToastGeneric"">
                        <text>{escTitulo}</text>
                        <text>{escLinha1}</text>
                        {(string.IsNullOrWhiteSpace(escLinha2) ? "" : $"<text>{escLinha2}</text>")}
                    </binding>
                </visual>
            </toast>";

            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var toast = new ToastNotification(doc);

            ToastNotifier notifier;
            try
            {
                notifier = ToastNotificationManager.CreateToastNotifier("ServsicPV");
            }
            catch
            {
                // Fallback AUMID garantido no Windows
                notifier = ToastNotificationManager.CreateToastNotifier("{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\\cmd.exe");
            }

            notifier.Show(toast);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationService] Erro no ToastNotificationManager.Show: {ex.Message}");
        }
    }
}
