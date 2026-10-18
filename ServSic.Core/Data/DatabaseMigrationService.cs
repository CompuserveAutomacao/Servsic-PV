using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace ServSic.Core.Data;

public static class DatabaseMigrationService
{
    private static bool _migracaoExecutada = false;

    /// <summary>
    /// Executa as migrações estruturais necessárias no banco de dados SQL Server e sincroniza arquivos INI.
    /// </summary>
    public static async Task ExecutarMigracaoAsync()
    {
        if (_migracaoExecutada) return;

        try
        {
            using var conn = DbConnectionFactory.CreateConnection();

            // 1. TabEst1.PerDescMax decimal(12,2) NULL default 0
            const string sqlTabEst1 = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TabEst1')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TabEst1' AND COLUMN_NAME = 'PerDescMax')
                    BEGIN
                        ALTER TABLE TabEst1 ADD PerDescMax decimal(12,2) NULL CONSTRAINT DF_TabEst1_PerDescMax DEFAULT 0;
                    END
                END";
            await conn.ExecuteAsync(sqlTabEst1);

            // 2. SERV.LkVendedor int NULL
            const string sqlServ = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SERV')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'SERV' AND COLUMN_NAME = 'LkVendedor')
                    BEGIN
                        ALTER TABLE SERV ADD LkVendedor int NULL;
                    END
                END";
            await conn.ExecuteAsync(sqlServ);

            // 3. CFG_VENDA.VendedorPorUsuario Char(1) default 'N'
            const string sqlCfgVenda = @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CFG_VENDA')
                BEGIN
                    CREATE TABLE CFG_VENDA (
                        Controle INT IDENTITY(1,1) PRIMARY KEY,
                        VendedorPorUsuario CHAR(1) NULL CONSTRAINT DF_CFG_VENDA_VendedorPorUsuario DEFAULT 'N'
                    );
                    INSERT INTO CFG_VENDA (VendedorPorUsuario) VALUES ('N');
                END
                ELSE
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CFG_VENDA' AND COLUMN_NAME = 'VendedorPorUsuario')
                    BEGIN
                        ALTER TABLE CFG_VENDA ADD VendedorPorUsuario CHAR(1) NULL CONSTRAINT DF_CFG_VENDA_VendedorPorUsuario DEFAULT 'N';
                        UPDATE CFG_VENDA SET VendedorPorUsuario = 'N' WHERE VendedorPorUsuario IS NULL;
                    END
                END";
            await conn.ExecuteAsync(sqlCfgVenda);

            // 4. TabEst3B.LkVendedor int NULL
            const string sqlTabEst3B = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TabEst3B')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TabEst3B' AND COLUMN_NAME = 'LkVendedor')
                    BEGIN
                        ALTER TABLE TabEst3B ADD LkVendedor int NULL;
                    END
                END";
            await conn.ExecuteAsync(sqlTabEst3B);

            // 5. Índices de performance para busca ultra rápida de produtos na TabEst1
            const string sqlIndicesTabEst1 = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TabEst1')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TabEst1_CodBarraCx' AND object_id = OBJECT_ID('TabEst1'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX IX_TabEst1_CodBarraCx ON TabEst1 (CodBarraCx);
                    END

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TabEst1_CodFornecedor' AND object_id = OBJECT_ID('TabEst1'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX IX_TabEst1_CodFornecedor ON TabEst1 (CodFornecedor);
                    END

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TabEst1_CodigoDirect' AND object_id = OBJECT_ID('TabEst1'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX IX_TabEst1_CodigoDirect ON TabEst1 (Codigo);
                    END
                END";
            await conn.ExecuteAsync(sqlIndicesTabEst1);

            // 6. Sincronização de INI (Venda/VendedorPorUsuario = N)
            SincronizarIniVendedorPorUsuario();

            _migracaoExecutada = true;
        }
        catch (Exception ex)
        {
            // Registra erro silenciosamente para não interromper inicialização
            System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationService] Erro ao executar migração: {ex.Message}");
        }
    }

    /// <summary>
    /// Sincroniza o parâmetro VendedorPorUsuario = N nas seções [Venda] de arquivos Config.ini
    /// </summary>
    private static void SincronizarIniVendedorPorUsuario()
    {
        string[] caminhosPossiveis = new[]
        {
            @"C:\Compuserve\Servsic\Config.ini",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.ini")
        };

        foreach (var path in caminhosPossiveis)
        {
            try
            {
                if (!File.Exists(path)) continue;

                string conteudo = File.ReadAllText(path, Encoding.GetEncoding("ISO-8859-1"));
                if (conteudo.Contains("VendedorPorUsuario", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Se existe a seção [Venda], insere abaixo; senão adiciona a seção [Venda]
                var sb = new StringBuilder(conteudo);
                int idxVenda = conteudo.IndexOf("[Venda]", StringComparison.OrdinalIgnoreCase);
                if (idxVenda >= 0)
                {
                    int fimLinha = conteudo.IndexOf('\n', idxVenda);
                    if (fimLinha >= 0)
                    {
                        sb.Insert(fimLinha + 1, "VendedorPorUsuario=N\r\n");
                    }
                    else
                    {
                        sb.AppendLine("VendedorPorUsuario=N");
                    }
                }
                else
                {
                    sb.AppendLine("\r\n[Venda]");
                    sb.AppendLine("VendedorPorUsuario=N");
                }

                File.WriteAllText(path, sb.ToString(), Encoding.GetEncoding("ISO-8859-1"));
            }
            catch
            {
                // Ignora falhas de gravação em diretórios restritos
            }
        }
    }

    /// <summary>
    /// Consulta se a regra de Vendedor por Usuário está ativa ('S') no banco ou no INI.
    /// </summary>
    public static async Task<bool> IsVendedorPorUsuarioAtivoAsync()
    {
        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            string? valorDb = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT TOP 1 VendedorPorUsuario FROM CFG_VENDA WHERE VendedorPorUsuario IS NOT NULL");

            if (!string.IsNullOrWhiteSpace(valorDb))
            {
                return valorDb.Trim().Equals("S", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Fallback para arquivo INI
        }

        // Fallback INI
        try
        {
            string path = @"C:\Compuserve\Servsic\Config.ini";
            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                bool inVenda = false;
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        inVenda = trimmed.Equals("[Venda]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (inVenda && trimmed.StartsWith("VendedorPorUsuario", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmed.Split('=');
                        if (parts.Length > 1)
                        {
                            return parts[1].Trim().Equals("S", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
        }
        catch { }

        return false;
    }
}
