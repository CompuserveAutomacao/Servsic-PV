using System;
using System.IO;
using System.Collections.Generic;

namespace ServSic.Core.Config;

public static class ServcomReader
{
    public static bool TryLoad(out ServcomConfig config, string? specificPath = null)
    {
        string resolvedPath = FindServcomFile(specificPath);
        if (!File.Exists(resolvedPath))
        {
            config = new ServcomConfig
            {
                FilePath = resolvedPath,
                Server = @".\qg",
                Database = "prod silcon",
                UserId = "sa",
                Password = "123"
            };
            return false;
        }

        try
        {
            string content = File.ReadAllText(resolvedPath).Trim();
            var pairs = ParseConnectionString(content);

            config = new ServcomConfig
            {
                FilePath = resolvedPath,
                RawConnectionString = content
            };

            if (pairs.TryGetValue("Data Source", out var server) || pairs.TryGetValue("Server", out server))
                config.Server = server;

            if (pairs.TryGetValue("Initial Catalog", out var database) || pairs.TryGetValue("Database", out database))
                config.Database = database;

            if (pairs.TryGetValue("User ID", out var user) || pairs.TryGetValue("UID", out user) || pairs.TryGetValue("User", out user))
                config.UserId = user;

            if (pairs.TryGetValue("Password", out var pwd) || pairs.TryGetValue("PWD", out pwd))
                config.Password = pwd;

            return true;
        }
        catch
        {
            config = new ServcomConfig
            {
                FilePath = resolvedPath,
                Server = @".\qg",
                Database = "prod silcon",
                UserId = "sa",
                Password = "123"
            };
            return false;
        }
    }

    public static ServcomConfig Load(string? specificPath = null)
    {
        if (TryLoad(out var config, specificPath))
        {
            return config;
        }

        throw new FileNotFoundException($"Arquivo Servcom.dll não foi encontrado nos locais pesquisados. Caminho base: '{AppDomain.CurrentDomain.BaseDirectory}'");
    }

    public static void Save(ServcomConfig config, string? specificPath = null)
    {
        string targetPath = !string.IsNullOrWhiteSpace(specificPath)
            ? specificPath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servcom.dll");

        string content = $"Provider=SQLOLEDB.1;Persist Security Info=False;User ID={config.UserId};Password={config.Password};Data Source={config.Server};Initial Catalog={config.Database}";

        File.WriteAllText(targetPath, content);
        config.FilePath = targetPath;
        config.RawConnectionString = content;

        // Se existir a pasta c:\DEV\SERVSIC PV, mantém sincronizado também no diretório de desenvolvimento
        const string devDir = @"c:\DEV\SERVSIC PV";
        if (Directory.Exists(devDir))
        {
            try
            {
                string devFile = Path.Combine(devDir, "Servcom.dll");
                if (!string.Equals(Path.GetFullPath(targetPath), Path.GetFullPath(devFile), StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(devFile, content);
                }
            }
            catch
            {
                // Ignora falhas secundárias de gravação em dev
            }
        }
    }

    public static async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(ServcomConfig config)
    {
        try
        {
            var connStr = config.BuildSqlConnectionString(timeoutSeconds: 8);
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
            await conn.OpenAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string FindServcomFile(string? specificPath)
    {
        if (!string.IsNullOrWhiteSpace(specificPath) && File.Exists(specificPath))
            return specificPath;

        var candidates = new List<string>
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servcom.dll"),
            Path.Combine(Directory.GetCurrentDirectory(), "Servcom.dll"),
            @"c:\DEV\SERVSIC PV\Servcom.dll"
        };

        // Adiciona diretórios pais até a raiz
        string current = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 4; i++)
        {
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            candidates.Add(Path.Combine(parent.FullName, "Servcom.dll"));
            current = parent.FullName;
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Servcom.dll");
    }

    private static Dictionary<string, string> ParseConnectionString(string connStr)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = connStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var eqIdx = part.IndexOf('=');
            if (eqIdx > 0)
            {
                var key = part.Substring(0, eqIdx).Trim();
                var value = part.Substring(eqIdx + 1).Trim();
                dict[key] = value;
            }
        }
        return dict;
    }
}
