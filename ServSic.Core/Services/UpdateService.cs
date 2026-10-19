using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ServSic.Core.Services;

public static class UpdateService
{
    private static readonly HttpClient HttpClient = new HttpClient();
    public const string Repository = "CompuserveAutomacao/Servsic-PV";

    public static string ObterVersaoAtual()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        if (version != null)
        {
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
        return "1.0.0";
    }

    public static async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            string url = $"https://api.github.com/repos/{Repository}/releases/latest";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "ServsicPV");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            string remoteVersionStr = release.TagName.TrimStart('v', 'V').Trim();
            if (!Version.TryParse(remoteVersionStr, out Version? remoteVersion))
                return null;

            string localVersionStr = ObterVersaoAtual();
            if (!Version.TryParse(localVersionStr, out Version? localVersion))
                localVersion = new Version(1, 0, 0);

            // Compara as versões: se a versão remota for superior, há atualização disponível
            if (remoteVersion > localVersion)
            {
                // Procura o asset do zip de atualização (servsicpv_update.zip ou qualquer .zip)
                var zipAsset = release.Assets.FirstOrDefault(a => 
                    a.Name.Equals("servsicpv_update.zip", StringComparison.OrdinalIgnoreCase))
                    ?? release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                if (zipAsset != null)
                {
                    return new UpdateInfo
                    {
                        CurrentVersion = $"v{localVersionStr}",
                        NewVersion = release.TagName.StartsWith("v", StringComparison.OrdinalIgnoreCase) 
                            ? release.TagName 
                            : $"v{release.TagName}",
                        Changelog = string.IsNullOrWhiteSpace(release.Body) 
                            ? "Melhorias gerais e correções de estabilidade." 
                            : release.Body.Trim(),
                        DownloadUrl = zipAsset.BrowserDownloadUrl,
                        ReleaseUrl = release.HtmlUrl
                    };
                }
            }
        }
        catch
        {
            // Silencia falhas de conexão/timeout para não impedir o uso do sistema em caso de rede instável
        }

        return null;
    }

    public static async Task ExecuteUpdateAsync(
        string downloadUrl, 
        Action<double>? progressCallback = null,
        Action<string>? statusCallback = null)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ServsicPVUpdate");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        string zipPath = Path.Combine(tempDir, "servsicpv_update.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        statusCallback?.Invoke("Conectando ao servidor de atualização do GitHub...");

        // 1. Download do arquivo com feedback de progresso
        using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
        {
            request.Headers.Add("User-Agent", "ServsicPV");
            using (var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalReadBytes = 0;
                    int readBytes;

                    while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, readBytes);
                        totalReadBytes += readBytes;

                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            double progress = (double)totalReadBytes / totalBytes.Value * 100;
                            progressCallback?.Invoke(progress);
                            statusCallback?.Invoke($"Baixando arquivos da nova versão... ({(int)progress}%)");
                        }
                    }
                }
            }
        }

        statusCallback?.Invoke("Download concluído! Reiniciando o SERVSIC PV em instantes para aplicar a nova versão...");
        progressCallback?.Invoke(100);
        await Task.Delay(2000);

        // 2. Geração do script atualizar.bat
        string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        string batPath = Path.Combine(tempDir, "atualizar.bat");
        if (File.Exists(batPath))
        {
            File.Delete(batPath);
        }

        string appExeName = Path.GetFileName(Environment.ProcessPath ?? "ServsicPV.exe");

        // O script batch aguarda o encerramento do processo, descompacta sobrescrevendo e preservando Servcom.dll
        string batContent = $@"@echo off
title Atualizando SERVSIC PV...
echo Aguardando o encerramento do SERVSIC PV...
timeout /t 2 /nobreak >nul

:: Forca o encerramento do SERVSIC PV se ainda estiver aberto
taskkill /f /im {appExeName} >nul 2>&1

:: Descompacta por cima da pasta de instalacao atual preservando Servcom.dll
echo Aplicando arquivos da nova versao...
tar -xf ""{zipPath}"" -C ""{currentDir}"" --exclude=""Servcom.dll""

:: Reabre a nova versao do SERVSIC PV
echo Iniciando versao atualizada...
start """" /d ""{currentDir}"" ""{appExeName}""

:: Limpa o zip e agenda a auto-exclusao do proprio script .bat
del /q ""{zipPath}"" >nul 2>&1
(goto) 2>nul & del ""%~f0""
";

        await File.WriteAllTextAsync(batPath, batContent);

        // 3. Executa o atualizar.bat e finaliza a aplicação
        var startInfo = new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = true,
            CreateNoWindow = false,
            WorkingDirectory = tempDir
        };

        Process.Start(startInfo);
        
        // Encerra imediatamente o processo atual do SERVSIC PV
        Environment.Exit(0);
    }
}

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = "";
    public string NewVersion { get; set; } = "";
    public string Changelog { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ReleaseUrl { get; set; } = "";
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}
