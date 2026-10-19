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

        // Se o executável estiver em uma subpasta da pasta Servsic (ex: C:\Servsic\PV\),
        // mantém atualizado também o Servcom.dll da pasta mãe do ERP legado
        try
        {
            var parent = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory);
            if (parent != null)
            {
                string parentFile = Path.Combine(parent.FullName, "Servcom.dll");
                if (File.Exists(parentFile) && !string.Equals(Path.GetFullPath(targetPath), Path.GetFullPath(parentFile), StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(parentFile, content);
                }
            }
        }
        catch { }

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
            return Path.GetFullPath(specificPath);

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string localFile = Path.Combine(baseDir, "Servcom.dll");

        var prioritizedCandidates = new List<string>();

        // 1. PRIORIDADE MÁXIMA: Pasta mãe do ERP Legado SERVSIC.
        // Cenário: o ServsicPV sempre fica em uma subpasta dentro da pasta Servsic (ex: C:\Servsic\PV\ ou C:\Servsic\ServsicPV\).
        // Ao copiar a pasta do PV para dentro da pasta do Servsic, busca e usa prioritariamente o Servcom.dll da pasta mãe.
        var directParent = Directory.GetParent(baseDir);
        if (directParent != null)
        {
            prioritizedCandidates.Add(Path.Combine(directParent.FullName, "Servcom.dll"));
        }

        // 2. Subindo na árvore de diretórios (até 4 níveis) procurando especificamente pasta chamada 'SERVSIC' ou 'Servsic'
        string current = baseDir;
        for (int i = 0; i < 4; i++)
        {
            var p = Directory.GetParent(current);
            if (p == null) break;

            if (p.Name.Equals("Servsic", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals("SERVSIC", StringComparison.OrdinalIgnoreCase))
            {
                prioritizedCandidates.Add(Path.Combine(p.FullName, "Servcom.dll"));
            }
            current = p.FullName;
        }

        // 3. Pastas padrões clássicas de instalação do ERP SERVSIC nos discos locais
        string[] padroesInstalacao =
        {
            @"C:\Servsic\Servcom.dll",
            @"C:\SERVSIC\Servcom.dll",
            @"D:\Servsic\Servcom.dll",
            @"D:\SERVSIC\Servcom.dll",
            @"E:\Servsic\Servcom.dll",
            @"E:\SERVSIC\Servcom.dll"
        };

        foreach (var padrao in padroesInstalacao)
        {
            prioritizedCandidates.Add(padrao);
        }

        // Se encontrou Servcom.dll na pasta mãe do ERP legado ou em locais padrões do Servsic:
        foreach (var candidate in prioritizedCandidates)
        {
            if (File.Exists(candidate))
            {
                string fullCandidate = Path.GetFullPath(candidate);
                SincronizarComLocal(fullCandidate, localFile);
                return fullCandidate;
            }
        }

        // 4. Se não achou na pasta mãe do ERP, utiliza o Servcom.dll presente na pasta local do PV
        if (File.Exists(localFile))
        {
            return Path.GetFullPath(localFile);
        }

        // 5. Diretório de trabalho atual (CurrentDirectory)
        string curDirFile = Path.Combine(Directory.GetCurrentDirectory(), "Servcom.dll");
        if (File.Exists(curDirFile))
        {
            return Path.GetFullPath(curDirFile);
        }

        // 6. Fallback para ambiente de desenvolvimento
        const string devPath = @"c:\DEV\SERVSIC PV\Servcom.dll";
        if (File.Exists(devPath))
        {
            return Path.GetFullPath(devPath);
        }

        return localFile;
    }

    private static void SincronizarComLocal(string sourceFile, string targetLocalFile)
    {
        try
        {
            if (string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(targetLocalFile), StringComparison.OrdinalIgnoreCase))
                return;

            string masterContent = File.ReadAllText(sourceFile);
            bool precisaAtualizar = !File.Exists(targetLocalFile) || File.ReadAllText(targetLocalFile).Trim() != masterContent.Trim();
            if (precisaAtualizar)
            {
                File.WriteAllText(targetLocalFile, masterContent);
            }
        }
        catch
        {
            // Ignora falha de permissão de escrita local caso a pasta esteja protegida
        }
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
