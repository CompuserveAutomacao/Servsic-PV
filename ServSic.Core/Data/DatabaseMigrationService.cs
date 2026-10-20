using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace ServSic.Core.Data;

public static class DatabaseMigrationService
{
    private static string? _ultimoBancoMigrado = null;
    private static readonly object _lock = new();

    /// <summary>
    /// Reseta o estado de migração para que seja reexecutada na próxima chamada ou troca de banco.
    /// </summary>
    public static void ResetMigrationState()
    {
        lock (_lock)
        {
            _ultimoBancoMigrado = null;
        }
    }

    /// <summary>
    /// Executa as migrações estruturais necessárias no banco de dados SQL Server e sincroniza arquivos INI.
    /// </summary>
    public static async Task ExecutarMigracaoAsync(bool forcar = false)
    {
        var config = DbConnectionFactory.GetConfig();
        string connKey = $"{config.Server?.Trim()}_{config.Database?.Trim()}";

        lock (_lock)
        {
            if (!forcar && !string.IsNullOrEmpty(_ultimoBancoMigrado) && _ultimoBancoMigrado == connKey)
            {
                return;
            }
        }

        try
        {
            using var conn = DbConnectionFactory.CreateConnection();

            // 1. TabEst1.PerDescMax decimal(12,2) NULL default 0
            const string sqlTabEst1 = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'TABEST1')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST1' AND UPPER(COLUMN_NAME) = 'PERDESCMAX')
                    BEGIN
                        ALTER TABLE TabEst1 ADD PerDescMax decimal(12,2) NULL CONSTRAINT DF_TabEst1_PerDescMax DEFAULT 0;
                    END
                END";
            await ExecutarPassoAsync(conn, "TabEst1.PerDescMax", sqlTabEst1);

            // 2. SERV.LkVendedor int NULL e Inativo bit NULL
            const string sqlServ = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'SERV')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'SERV' AND UPPER(COLUMN_NAME) = 'LKVENDEDOR')
                    BEGIN
                        ALTER TABLE SERV ADD LkVendedor int NULL;
                    END
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'SERV' AND UPPER(COLUMN_NAME) = 'INATIVO')
                    BEGIN
                        ALTER TABLE SERV ADD Inativo bit NULL CONSTRAINT DF_SERV_Inativo DEFAULT 0;
                    END
                END";
            await ExecutarPassoAsync(conn, "SERV.LkVendedor e Inativo", sqlServ);

            // 3. CFG_VENDA.VendedorPorUsuario Char(1) default 'N'
            const string sqlCfgVenda = @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'CFG_VENDA')
                BEGIN
                    CREATE TABLE CFG_VENDA (
                        Controle INT IDENTITY(1,1) PRIMARY KEY,
                        VendedorPorUsuario CHAR(1) NULL CONSTRAINT DF_CFG_VENDA_VendedorPorUsuario DEFAULT 'N'
                    );
                    INSERT INTO CFG_VENDA (VendedorPorUsuario) VALUES ('N');
                END
                ELSE
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'CFG_VENDA' AND UPPER(COLUMN_NAME) = 'VENDEDORPORUSUARIO')
                    BEGIN
                        ALTER TABLE CFG_VENDA ADD VendedorPorUsuario CHAR(1) NULL CONSTRAINT DF_CFG_VENDA_VendedorPorUsuario DEFAULT 'N';
                        UPDATE CFG_VENDA SET VendedorPorUsuario = 'N' WHERE VendedorPorUsuario IS NULL;
                    END
                END";
            await ExecutarPassoAsync(conn, "CFG_VENDA", sqlCfgVenda);

            // 3b. CFG_ORCAMENTO.ESTOQUEINSUFICIENTE VARCHAR(1) default 'N'
            const string sqlCfgOrcamento = @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'CFG_ORCAMENTO')
                BEGIN
                    CREATE TABLE CFG_ORCAMENTO (
                        ESTOQUEINSUFICIENTE VARCHAR(1) NULL CONSTRAINT DF_CFG_ORCAMENTO_ESTOQUEINSUFICIENTE DEFAULT 'N'
                    );
                    INSERT INTO CFG_ORCAMENTO (ESTOQUEINSUFICIENTE) VALUES ('N');
                END
                ELSE
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'CFG_ORCAMENTO' AND UPPER(COLUMN_NAME) = 'ESTOQUEINSUFICIENTE')
                    BEGIN
                        ALTER TABLE CFG_ORCAMENTO ADD ESTOQUEINSUFICIENTE VARCHAR(1) NULL CONSTRAINT DF_CFG_ORCAMENTO_ESTOQUEINSUFICIENTE DEFAULT 'N';
                    END
                END";
            await ExecutarPassoAsync(conn, "CFG_ORCAMENTO", sqlCfgOrcamento);

            // 3c. CFG_PREVENDA.ESTOQUEINSUFICIENTE VARCHAR(1) default 'N'
            const string sqlCfgPreVenda = @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'CFG_PREVENDA')
                BEGIN
                    CREATE TABLE CFG_PREVENDA (
                        ESTOQUEINSUFICIENTE VARCHAR(1) NULL CONSTRAINT DF_CFG_PREVENDA_ESTOQUEINSUFICIENTE DEFAULT 'N'
                    );
                    INSERT INTO CFG_PREVENDA (ESTOQUEINSUFICIENTE) VALUES ('N');
                END
                ELSE
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'CFG_PREVENDA' AND UPPER(COLUMN_NAME) = 'ESTOQUEINSUFICIENTE')
                    BEGIN
                        ALTER TABLE CFG_PREVENDA ADD ESTOQUEINSUFICIENTE VARCHAR(1) NULL CONSTRAINT DF_CFG_PREVENDA_ESTOQUEINSUFICIENTE DEFAULT 'N';
                    END
                END";
            await ExecutarPassoAsync(conn, "CFG_PREVENDA", sqlCfgPreVenda);

            // 4. TabEst3B.LkVendedor int NULL
            const string sqlTabEst3B = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'TABEST3B')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3B' AND UPPER(COLUMN_NAME) = 'LKVENDEDOR')
                    BEGIN
                        ALTER TABLE TabEst3B ADD LkVendedor int NULL;
                    END
                END";
            await ExecutarPassoAsync(conn, "TabEst3B.LkVendedor", sqlTabEst3B);

            // 5. TabEst3C.RECEBIMENTO VARCHAR(40) para acomodar formas e parcelas completas
            const string sqlTabEst3C = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3C' AND UPPER(COLUMN_NAME) = 'RECEBIMENTO' AND CHARACTER_MAXIMUM_LENGTH < 40)
                BEGIN
                    ALTER TABLE TabEst3C ALTER COLUMN RECEBIMENTO VARCHAR(40);
                END";
            await ExecutarPassoAsync(conn, "TabEst3C.RECEBIMENTO", sqlTabEst3C);

            // 6. TabEst3B.Unidade VARCHAR(10) para acomodar unidades como PT100, UNID, PCTE
            const string sqlTabEst3BUnid = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3B' AND UPPER(COLUMN_NAME) = 'UNIDADE' AND CHARACTER_MAXIMUM_LENGTH < 10)
                BEGIN
                    ALTER TABLE TabEst3B ALTER COLUMN Unidade VARCHAR(10);
                END";
            await ExecutarPassoAsync(conn, "TabEst3B.Unidade", sqlTabEst3BUnid);

            // 7. Índices de performance para busca ultra rápida de produtos na TabEst1 (com verificação de coluna)
            const string sqlIndicesTabEst1 = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'TABEST1')
                BEGIN
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST1' AND UPPER(COLUMN_NAME) = 'CODBARRACX')
                       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TabEst1_CodBarraCx' AND object_id = OBJECT_ID('TabEst1'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX IX_TabEst1_CodBarraCx ON TabEst1 (CodBarraCx);
                    END

                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST1' AND UPPER(COLUMN_NAME) = 'CODFORNECEDOR')
                       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TabEst1_CodFornecedor' AND object_id = OBJECT_ID('TabEst1'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX IX_TabEst1_CodFornecedor ON TabEst1 (CodFornecedor);
                    END

                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST1' AND UPPER(COLUMN_NAME) = 'CODIGO')
                       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TabEst1_CodigoDirect' AND object_id = OBJECT_ID('TabEst1'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX IX_TabEst1_CodigoDirect ON TabEst1 (Codigo);
                    END
                END";
            await ExecutarPassoAsync(conn, "TabEst1.Indices", sqlIndicesTabEst1);

            // 8. ServNivelLimite (Tabela de limites de desconto por nível de usuário: Operador, Supervisor, Gerente)
            const string sqlServNivelLimite = @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'SERVNIVELLIMITE')
                BEGIN
                    CREATE TABLE ServNivelLimite (
                        Controle INT IDENTITY(1,1) PRIMARY KEY,
                        IdEmpresa INT NULL,
                        DataHora DATETIME NULL,
                        OperadorDescVen FLOAT NULL CONSTRAINT DF_ServNivelLimite_OperadorDescVen DEFAULT 3.0,
                        OperadorDescRec FLOAT NULL CONSTRAINT DF_ServNivelLimite_OperadorDescRec DEFAULT 0.0,
                        SupervisorDescVen FLOAT NULL CONSTRAINT DF_ServNivelLimite_SupervisorDescVen DEFAULT 3.0,
                        SupervisorDescRec FLOAT NULL CONSTRAINT DF_ServNivelLimite_SupervisorDescRec DEFAULT 0.0,
                        GerenteDescVen FLOAT NULL CONSTRAINT DF_ServNivelLimite_GerenteDescVen DEFAULT 5.0,
                        GerenteDescRec FLOAT NULL CONSTRAINT DF_ServNivelLimite_GerenteDescRec DEFAULT 50.0
                    );
                    INSERT INTO ServNivelLimite (IdEmpresa, DataHora, OperadorDescVen, SupervisorDescVen, GerenteDescVen)
                    VALUES (1, GETDATE(), 3.0, 3.0, 5.0);
                END";
            await ExecutarPassoAsync(conn, "ServNivelLimite", sqlServNivelLimite);

            // 9. Multi-Endereço do Cliente: dbo.TabCliEndereco
            const string sqlTabCliEndereco = @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'TABCLIENDERECO')
                BEGIN
                    CREATE TABLE dbo.TabCliEndereco (
                        Controle          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        LkCliente         INT NOT NULL,
                        Apelido           VARCHAR(60) NOT NULL,
                        Endereco          VARCHAR(80) NULL,
                        Numero            VARCHAR(20) NULL,
                        ComplEnd          VARCHAR(60) NULL,
                        Bairro            VARCHAR(40) NULL,
                        Cidade            VARCHAR(40) NULL,
                        Estado            CHAR(2) NULL,
                        Cep               VARCHAR(10) NULL,
                        CodMunicipio      INT NULL,
                        Obs               VARCHAR(120) NULL,
                        Inativo           BIT NOT NULL DEFAULT 0,
                        DtUltAtualizacao  DATETIME NULL
                    );
                END";
            await ExecutarPassoAsync(conn, "TabCliEndereco.Tabela", sqlTabCliEndereco);

            const string sqlTabCliEnderecoIdx = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'TABCLIENDERECO')
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TabCliEndereco_LkCliente' AND object_id = OBJECT_ID('dbo.TabCliEndereco'))
                BEGIN
                    CREATE INDEX IX_TabCliEndereco_LkCliente ON dbo.TabCliEndereco (LkCliente, Inativo);
                END";
            await ExecutarPassoAsync(conn, "TabCliEndereco.Indice", sqlTabCliEnderecoIdx);

            // 10. TabEst3A: LkEnderecoCli, DataEntrega, LkEntregador, LkEntregaTx, VlEntregaTx
            const string sqlTabEst3ALkEndereco = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'TABEST3A')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3A' AND UPPER(COLUMN_NAME) = 'LKENDERECOCLI')
                    BEGIN
                        ALTER TABLE TabEst3A ADD LkEnderecoCli INT NULL;
                    END

                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3A' AND UPPER(COLUMN_NAME) = 'DATAENTREGA')
                    BEGIN
                        ALTER TABLE TabEst3A ADD DataEntrega DATETIME NULL;
                    END

                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3A' AND UPPER(COLUMN_NAME) = 'LKENTREGADOR')
                    BEGIN
                        ALTER TABLE TabEst3A ADD LkEntregador INT NULL;
                    END

                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3A' AND UPPER(COLUMN_NAME) = 'LKENTREGATX')
                    BEGIN
                        ALTER TABLE TabEst3A ADD LkEntregaTx INT NULL;
                    END

                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3A' AND UPPER(COLUMN_NAME) = 'VLENTREGATX')
                    BEGIN
                        ALTER TABLE TabEst3A ADD VlEntregaTx DECIMAL(12,2) NULL;
                    END

                    -- Amplia Telefone para no mínimo VARCHAR(20) para acomodar celulares com DDD formatados
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3A' AND UPPER(COLUMN_NAME) = 'TELEFONE' AND CHARACTER_MAXIMUM_LENGTH < 20)
                    BEGIN
                        ALTER TABLE TabEst3A ALTER COLUMN Telefone VARCHAR(20);
                    END
                END";
            await ExecutarPassoAsync(conn, "TabEst3A.LkEnderecoCli e Campos Entrega", sqlTabEst3ALkEndereco);

            // 11. TabEst3B.Entrega int NULL default 0
            const string sqlTabEst3BEntrega = @"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE UPPER(TABLE_NAME) = 'TABEST3B')
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE UPPER(TABLE_NAME) = 'TABEST3B' AND UPPER(COLUMN_NAME) = 'ENTREGA')
                    BEGIN
                        ALTER TABLE TabEst3B ADD Entrega INT NULL DEFAULT 0;
                    END
                END";
            await ExecutarPassoAsync(conn, "TabEst3B.Entrega", sqlTabEst3BEntrega);

            // 12. Sincronização de INI (Venda/VendedorPorUsuario = N)
            SincronizarIniVendedorPorUsuario();

            lock (_lock)
            {
                _ultimoBancoMigrado = connKey;
            }
        }
        catch (Exception ex)
        {
            // Registra erro silenciosamente para não interromper inicialização
            System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationService] Erro ao conectar banco para migração: {ex.Message}");
        }
    }

    private static async Task ExecutarPassoAsync(System.Data.IDbConnection conn, string nomePasso, string sql)
    {
        try
        {
            await conn.ExecuteAsync(sql);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseMigrationService] Aviso/Erro no passo '{nomePasso}': {ex.Message}");
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
