using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço responsável exclusivamente pelas operações no cabeçalho do pedido/orçamento (TabEst3A).
/// </summary>
public class PedidoCabService
{
    public async Task<PedidoVenda?> ObterPorIdAsync(int controle)
    {
        const string sqlHeader = @"
            SELECT TOP 1
                ta.Controle,
                ta.Pedido,
                ta.Data,
                ta.HoraIni,
                ta.Cliente,
                ta.LkCliente,
                ta.Vendedor,
                ta.LkVendedor,
                v.Celular as VendedorCelular,
                v.Telefone as VendedorTelefone,
                ta.TOTAL as Total,
                ta.DESCONTO as Desconto,
                ta.ACRESCIMO as Acrescimo,
                ta.SUBTOTAL as Subtotal,
                ta.Vlpago as VlPago,
                ISNULL(ta.Entrada, 0) as Entrada,
                ta.LkCxFecha as LkCxFecha,
                ta.QtdItens,
                ta.Status,
                ta.Venda,
                ta.Cancelada,
                ta.LkUsuario,
                ta.idEmpresa as IdEmpresa,
                ta.Endereco,
                ta.Numero,
                ta.Bairro,
                ta.Cidade,
                ta.UF,
                ta.CEP,
                ta.CPF_CNPJ,
                ta.Telefone,
                ta.eMail as Email,
                ta.ComplEnd,
                ta.Obs,
                ta.LkEnderecoCli,
                ta.DataEntrega,
                ta.LkEntregador,
                ta.LkEntregaTx,
                ta.VlEntregaTx,
                c.RefBan as ClienteRefBan,
                c.Obs as ClienteObs
            FROM TabEst3A ta
            LEFT JOIN TabVend v ON ta.LkVendedor = v.CONTROLE
            LEFT JOIN TabCli c ON (ta.LkCliente IS NOT NULL AND ta.LkCliente > 0 AND ta.LkCliente = c.Controle)
            WHERE ta.Controle = @controle";

        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<PedidoVenda>(sqlHeader, new { controle });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 207 || ex.Message.Contains("LkEnderecoCli", StringComparison.OrdinalIgnoreCase))
        {
            await DatabaseMigrationService.ExecutarMigracaoAsync(forcar: true);
            using var connRetry = DbConnectionFactory.CreateConnection();
            return await connRetry.QueryFirstOrDefaultAsync<PedidoVenda>(sqlHeader, new { controle });
        }
    }

    public async Task<IReadOnlyList<PedidoVenda>> ListarAsync(
        string? status = null,
        string? search = null,
        DateTime? dataInicio = null,
        DateTime? dataFim = null,
        int limite = 100,
        int? lkUsuario = null,
        int? lkVendedor = null)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("limite", limite);

        var filters = new List<string> { "1=1" };

        if (lkUsuario.HasValue && lkUsuario.Value > 0)
        {
            if (lkVendedor.HasValue && lkVendedor.Value > 0)
            {
                filters.Add("(ta.LkUsuario = @lkUsuario OR ta.LkVendedor = @lkVendedor)");
                parameters.Add("lkUsuario", lkUsuario.Value);
                parameters.Add("lkVendedor", lkVendedor.Value);
            }
            else
            {
                filters.Add("ta.LkUsuario = @lkUsuario");
                parameters.Add("lkUsuario", lkUsuario.Value);
            }
        }
        else if (lkVendedor.HasValue && lkVendedor.Value > 0)
        {
            filters.Add("ta.LkVendedor = @lkVendedor");
            parameters.Add("lkVendedor", lkVendedor.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            string s = status.Trim().ToUpperInvariant();
            if (s == "O")
            {
                filters.Add("ta.Status = 'O' AND ISNULL(ta.Cancelada, 0) = 0");
            }
            else if (s == "P")
            {
                filters.Add("ta.Status = 'P' AND ISNULL(ta.Cancelada, 0) = 0");
            }
            else if (s == "V")
            {
                filters.Add("(ta.Status = 'V' OR ISNULL(ta.Venda, 0) = 1) AND ISNULL(ta.Cancelada, 0) = 0");
            }
            else if (s == "C")
            {
                filters.Add("(ISNULL(ta.Cancelada, 0) = 1 OR ta.Status = 'C')");
            }
            else
            {
                filters.Add("ta.Status = @status");
                parameters.Add("status", status.Trim());
            }
        }

        if (dataInicio.HasValue)
        {
            filters.Add("ta.Data >= @dataInicio");
            parameters.Add("dataInicio", dataInicio.Value.Date);
        }

        if (dataFim.HasValue)
        {
            filters.Add("ta.Data <= @dataFim");
            parameters.Add("dataFim", dataFim.Value.Date.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add("(ta.Cliente LIKE @search OR CAST(ta.Pedido AS VARCHAR) LIKE @search OR ta.CPF_CNPJ LIKE @search)");
            parameters.Add("search", $"%{search.Trim()}%");
        }

        string whereSql = string.Join(" AND ", filters);

        string sql = $@"
            SELECT TOP (@limite)
                ta.Controle,
                ta.Pedido,
                ta.Data,
                ta.HoraIni,
                ta.Cliente,
                ta.LkCliente,
                ta.Vendedor,
                ta.LkVendedor,
                ta.TOTAL as Total,
                ta.DESCONTO as Desconto,
                ta.ACRESCIMO as Acrescimo,
                ta.SUBTOTAL as Subtotal,
                ta.Vlpago as VlPago,
                ISNULL(ta.Entrada, 0) as Entrada,
                ISNULL(ta.Faturado, 0) as Faturado,
                ISNULL(ta.Nota, '') as Nota,
                ta.QtdItens,
                ta.Status,
                ta.Venda,
                ta.Cancelada,
                ta.LkUsuario,
                ta.idEmpresa as IdEmpresa,
                ta.Obs
            FROM TabEst3A ta
            WHERE {whereSql}
            ORDER BY ta.Data DESC, ta.Controle DESC";

        var list = await conn.QueryAsync<PedidoVenda>(sql, parameters);
        return list.ToList();
    }

    public async Task<int> SalvarCabecalhoAsync(PedidoVenda pedido, IDbConnection conn, IDbTransaction trans)
    {
        if (pedido.Data == default) pedido.Data = DateTime.Today;
        if (string.IsNullOrWhiteSpace(pedido.HoraIni)) pedido.HoraIni = DateTime.Now.ToString("HH:mm");
        if (pedido.IdEmpresa <= 0 || pedido.IdEmpresa == 1)
        {
            var empAtiva = await conn.ExecuteScalarAsync<int?>(
                "SELECT TOP 1 idEmpresa FROM TabEst3A WHERE idEmpresa IS NOT NULL AND idEmpresa > 0 ORDER BY Controle DESC",
                transaction: trans);
            pedido.IdEmpresa = (empAtiva.HasValue && empAtiva.Value > 0) ? empAtiva.Value : 2;
        }

        if (pedido.LkCaixa <= 0) pedido.LkCaixa = 1;
        if (string.IsNullOrWhiteSpace(pedido.Status) || pedido.Status == "V") pedido.Status = pedido.IsOrcamento ? "O" : "P";
        pedido.Venda = 0; // Sempre balcão (SERVSIC PV é frente de pré-venda/orçamento)

        // Regra do ERP Legado: Pré-Vendas ('P') recebem bloqueado = 3 para aguardar caixa
        pedido.Bloqueado = string.Equals(pedido.Status, "P", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        if (pedido.LkConvenio <= 0) pedido.LkConvenio = 999;
        pedido.LkTipo = 0;

        // Limite de crédito do cliente
        if (pedido.SaldoLimite <= 0 && pedido.LkCliente.HasValue && pedido.LkCliente.Value > 0)
        {
            var limiteCli = await conn.ExecuteScalarAsync<decimal?>(
                "SELECT TOP 1 LimiteCred FROM TABCLI WHERE Controle = @LkCliente",
                new { pedido.LkCliente }, transaction: trans);
            if (limiteCli.HasValue) pedido.SaldoLimite = limiteCli.Value;
        }

        // Cálculo exato de comissão da nota sincronizado com o percentual do vendedor na TabVend
        if (pedido.LkVendedor.HasValue && pedido.LkVendedor.Value > 0)
        {
            var comissaoPerc = await conn.ExecuteScalarAsync<double?>(
                "SELECT TOP 1 Comissao FROM TabVend WHERE Controle = @LkVendedor",
                new { pedido.LkVendedor }, transaction: trans);
            if (comissaoPerc.HasValue && comissaoPerc.Value > 0)
            {
                pedido.ComissaoNota = Math.Round(pedido.Total * ((decimal)comissaoPerc.Value / 100m), 5);
            }
        }

        // Sanitização defensiva contra erro de truncamento de strings no SQL Server
        static string? Trunca(string? val, int maxLen)
        {
            if (string.IsNullOrEmpty(val)) return val;
            return val.Length > maxLen ? val.Substring(0, maxLen) : val;
        }

        pedido.Cliente = Trunca(pedido.Cliente?.Trim(), 100);
        pedido.Vendedor = Trunca(pedido.Vendedor?.Trim(), 40);
        pedido.Telefone = Trunca(pedido.Telefone?.Trim(), 30);
        pedido.Obs = Trunca(pedido.Obs?.Trim(), 500);
        pedido.Codigo = Trunca(pedido.Codigo?.Trim(), 8);
        pedido.CPF_CNPJ = Trunca(pedido.CPF_CNPJ?.Trim(), 20);
        pedido.CEP = Trunca(pedido.CEP?.Trim(), 10);
        pedido.Cidade = Trunca(pedido.Cidade?.Trim(), 40);
        pedido.Bairro = Trunca(pedido.Bairro?.Trim(), 50);
        pedido.Endereco = Trunca(pedido.Endereco?.Trim(), 80);
        pedido.ComplEnd = Trunca(pedido.ComplEnd?.Trim(), 80);
        pedido.Numero = Trunca(pedido.Numero?.Trim(), 10);
        pedido.UF = Trunca(pedido.UF?.Trim(), 2);
        pedido.Email = Trunca(pedido.Email?.Trim(), 40);
        pedido.HoraIni = Trunca(pedido.HoraIni?.Trim(), 5);

        bool isNovo = false;

        if (pedido.Controle > 0)
        {
            var existe = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM TabEst3A WHERE Controle = @Controle",
                new { pedido.Controle }, transaction: trans);
            isNovo = (existe == 0);
        }
        else
        {
            isNovo = true;
            pedido.Pedido = 0; // Garante que novo pedido receba número virgem exclusivo
        }

        const string sqlUpdateA = @"
            UPDATE TabEst3A SET
                Codigo = @Codigo,
                Data = @Data,
                HoraIni = @HoraIni,
                Cliente = @Cliente,
                LkCliente = @LkCliente,
                Vendedor = @Vendedor,
                LkVendedor = @LkVendedor,
                TOTAL = @Total,
                DESCONTO = @Desconto,
                ACRESCIMO = @Acrescimo,
                SUBTOTAL = @Subtotal,
                Vlpago = @VlPago,
                Entrada = @Entrada,
                Faturado = @Faturado,
                QtdItens = @QtdItens,
                Status = @Status,
                Venda = @Venda,
                Cancelada = @Cancelada,
                LkUsuario = @LkUsuario,
                LkCaixa = @LkCaixa,
                LkCxFecha = @LkCxFecha,
                Endereco = @Endereco,
                Numero = @Numero,
                Bairro = @Bairro,
                Cidade = @Cidade,
                UF = @UF,
                CEP = @CEP,
                CPF_CNPJ = @CPF_CNPJ,
                Telefone = @Telefone,
                eMail = @Email,
                ComplEnd = @ComplEnd,
                Obs = @Obs,
                bloqueado = @Bloqueado,
                Saldo_limite = @SaldoLimite,
                lkConvenio = @LkConvenio,
                ComissaoNota = @ComissaoNota,
                ComissaoItemValor = @ComissaoItemValor,
                LkTipo = @LkTipo,
                LkEnderecoCli = @LkEnderecoCli,
                DataEntrega = @DataEntrega,
                LkEntregador = @LkEntregador,
                LkEntregaTx = @LkEntregaTx,
                VlEntregaTx = @VlEntregaTx
            WHERE Controle = @Controle;";

        if (isNovo)
        {
            // Arquitetura Ultra-Segura de Sequenciamento Concorrente:
            // 1. sp_getapplock: Serializa acessos simultâneos de qualquer terminal do ServSic PV
            // 2. WITH (TABLOCKX, HOLDLOCK) na TabNumPed: Isola contra o ERP legado que roda NovoPedido
            // 3. INSERT INTO TabNumPed + SCOPE_IDENTITY(): Obtém o IDENTITY real atômico do SQL Server
            // 4. WHILE EXISTS (TabEst3A): Proteção total contra duplicidades históricas
            // 5. UPDATE TabNum SET N_Nota: Sincronização 100% com o ERP legado
            // 6. Geração do Controle da TabEst3A garantida contra race conditions
            const string sqlGerarSequencia = @"
                -- A. Trava de aplicação distribuída exclusiva para este escopo transacional
                DECLARE @lockResult INT;
                EXEC @lockResult = sp_getapplock 
                    @Resource = 'SERVSIC_SEQUENCIA_PEDIDO', 
                    @LockMode = 'Exclusive', 
                    @LockOwner = 'Transaction', 
                    @LockTimeout = 15000;

                IF @lockResult < 0
                BEGIN
                    RAISERROR('Tempo limite excedido ao aguardar bloqueio para geração de pedido.', 16, 1);
                    RETURN;
                END

                -- B. Trava exclusiva da TabNumPed para que o ERP legado aguarde a gravação
                DECLARE @dummy INT;
                SELECT TOP 1 @dummy = 1 FROM TabNumPed WITH (TABLOCKX, HOLDLOCK);

                -- C. Inserção na TabNumPed para obter o IDENTITY atômico real gerado pelo SQL Server
                DECLARE @NumPed INT;
                DECLARE @User INT = ISNULL(@LkUsuario, 1);
                IF @User <= 0 SET @User = 1;

                INSERT INTO TabNumPed (LkUser, Data) VALUES (@User, GETDATE());
                SET @NumPed = SCOPE_IDENTITY();

                -- D. Verificação defensiva se o número já existe na TabEst3A
                WHILE EXISTS (SELECT 1 FROM TabEst3A WHERE Pedido = @NumPed)
                BEGIN
                    INSERT INTO TabNumPed (LkUser, Data) VALUES (@User, GETDATE());
                    SET @NumPed = SCOPE_IDENTITY();
                END

                -- E. Atualização da TabNum exigida pelo ERP legado
                UPDATE TabNum SET N_Nota = @NumPed;

                -- F. Geração do Controle exclusivo da TabEst3A
                DECLARE @NewControle INT;
                BEGIN TRY
                    EXEC NewCod_TabEst3A @NewControle OUTPUT;
                END TRY
                BEGIN CATCH
                    SELECT @NewControle = ISNULL(MAX(Controle), 0) + 1 FROM TabEst3A WITH (TABLOCKX, HOLDLOCK);
                END CATCH

                WHILE EXISTS (SELECT 1 FROM TabEst3A WHERE Controle = @NewControle)
                BEGIN
                    SET @NewControle = @NewControle + 1;
                END

                SELECT @NumPed AS Pedido, @NewControle AS Controle;";

            var seq = await conn.QueryFirstOrDefaultAsync<dynamic>(
                sqlGerarSequencia, 
                new { pedido.LkUsuario }, 
                transaction: trans);

            if (seq == null)
            {
                throw new InvalidOperationException("Falha crítica ao obter sequência exclusiva de pedido e controle no banco de dados.");
            }

            pedido.Pedido = Convert.ToInt32(seq.Pedido);
            pedido.Controle = Convert.ToInt32(seq.Controle);

            if (string.IsNullOrWhiteSpace(pedido.Codigo) && pedido.LkCliente.HasValue)
            {
                pedido.Codigo = pedido.LkCliente.Value.ToString("D6");
            }

            // Proteção contra duplicação de PK (Pedido, IdEmpresa, Controle)
            var jaExistePK = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM TabEst3A WHERE Pedido = @Pedido AND idEmpresa = @IdEmpresa AND Controle = @Controle",
                new { pedido.Pedido, pedido.IdEmpresa, pedido.Controle }, transaction: trans);

            if (jaExistePK > 0)
            {
                await conn.ExecuteAsync(sqlUpdateA, pedido, transaction: trans);
                return pedido.Controle;
            }

            const string sqlInsertA = @"
                INSERT INTO TabEst3A (
                    Controle, Pedido, Codigo, Data, HoraIni, Cliente, LkCliente, Vendedor, LkVendedor,
                    TOTAL, DESCONTO, ACRESCIMO, SUBTOTAL, Vlpago, Entrada, Faturado, QtdItens, Status, Venda, Cancelada,
                    LkUsuario, LkCaixa, LkCxFecha, idEmpresa, Endereco, Numero, Bairro, Cidade, UF, CEP, CPF_CNPJ,
                    Telefone, eMail, ComplEnd, Obs, bloqueado, Saldo_limite, lkConvenio, ComissaoNota, ComissaoItemValor, LkTipo,
                    DataEntrega, LkEnderecoCli, LkEntregador, LkEntregaTx, VlEntregaTx
                ) VALUES (
                    @Controle, @Pedido, @Codigo, @Data, @HoraIni, @Cliente, @LkCliente, @Vendedor, @LkVendedor,
                    @Total, @Desconto, @Acrescimo, @Subtotal, @VlPago, @Entrada, @Faturado, @QtdItens, @Status, @Venda, @Cancelada,
                    @LkUsuario, @LkCaixa, @LkCxFecha, @IdEmpresa, @Endereco, @Numero, @Bairro, @Cidade, @UF, @CEP, @CPF_CNPJ,
                    @Telefone, @Email, @ComplEnd, @Obs, @Bloqueado, @SaldoLimite, @LkConvenio, @ComissaoNota, @ComissaoItemValor, @LkTipo,
                    ISNULL(@DataEntrega, GETDATE()), @LkEnderecoCli, @LkEntregador, @LkEntregaTx, @VlEntregaTx
                );";

            await conn.ExecuteAsync(sqlInsertA, pedido, transaction: trans);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(pedido.Codigo) && pedido.LkCliente.HasValue)
            {
                pedido.Codigo = pedido.LkCliente.Value.ToString("D6");
            }

            await conn.ExecuteAsync(sqlUpdateA, pedido, transaction: trans);
        }

        return pedido.Controle;
    }



    public async Task<bool> ConverterEmVendaAsync(int controle)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE TabEst3A SET
                Status = 'P',
                Venda = 0
            WHERE Controle = @controle AND Status = 'O';";

        int affected = await conn.ExecuteAsync(sql, new { controle });
        return affected > 0;
    }

    public async Task<bool> CancelarAsync(int controle)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE TabEst3A SET
                Cancelada = 1
            WHERE Controle = @controle;";

        int affected = await conn.ExecuteAsync(sql, new { controle });
        return affected > 0;
    }
}
