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
        using var conn = DbConnectionFactory.CreateConnection();
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
                ta.Obs
            FROM TabEst3A ta
            WHERE ta.Controle = @controle";

        return await conn.QueryFirstOrDefaultAsync<PedidoVenda>(sqlHeader, new { controle });
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
        if (pedido.IdEmpresa <= 0) pedido.IdEmpresa = 1;
        if (pedido.LkCaixa <= 0) pedido.LkCaixa = 1;
        if (string.IsNullOrWhiteSpace(pedido.Status)) pedido.Status = pedido.IsOrcamento ? "O" : "P";
        pedido.Venda = 0; // Sempre balcão

        // Sanitização defensiva contra erro de truncamento de strings no SQL Server
        if (pedido.Cliente?.Length > 40) pedido.Cliente = pedido.Cliente.Substring(0, 40);
        if (pedido.Vendedor?.Length > 20) pedido.Vendedor = pedido.Vendedor.Substring(0, 20);
        if (pedido.Obs?.Length > 200) pedido.Obs = pedido.Obs.Substring(0, 200);
        if (pedido.Codigo?.Length > 8) pedido.Codigo = pedido.Codigo.Substring(0, 8);

        bool isNovo = pedido.Controle <= 0;

        if (isNovo)
        {
            const string sqlNewCodA = @"
                DECLARE @NewCod INT;
                BEGIN TRY
                    EXEC NewCod_TabEst3A @NewCod OUTPUT;
                END TRY
                BEGIN CATCH
                    SELECT @NewCod = ISNULL(MAX(Controle), 0) + 1 FROM TabEst3A;
                END CATCH
                SELECT @NewCod;";

            pedido.Controle = await conn.ExecuteScalarAsync<int>(sqlNewCodA, transaction: trans);

            if (pedido.Pedido <= 0)
            {
                const string sqlNextPedido = @"
                    DECLARE @NumPed INT;
                    DECLARE @User INT = ISNULL(@LkUsuario, 1);
                    IF @User <= 0 SET @User = 1;

                    BEGIN TRY
                        EXEC NovoPedido @NumPed OUTPUT, @User;
                    END TRY
                    BEGIN CATCH
                        -- Fallback sincronizado com a sequência oficial do ERP
                        SELECT @NumPed = ISNULL(MAX(P), 0) + 1 
                        FROM (
                            SELECT ISNULL(MAX(Pedido), 0) AS P FROM TabNumPed
                            UNION ALL
                            SELECT ISNULL(MAX(N_Nota), 0) AS P FROM TabNum
                            UNION ALL
                            SELECT ISNULL(MAX(Pedido), 0) AS P FROM TabEst3A
                        ) T;

                        IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TabNumPed')
                        BEGIN
                            INSERT INTO TabNumPed (LkUser, Data) VALUES (@User, GETDATE());
                        END

                        IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TabNum')
                        BEGIN
                            UPDATE TabNum SET N_Nota = @NumPed;
                        END
                    END CATCH

                    SELECT @NumPed;";
                pedido.Pedido = await conn.ExecuteScalarAsync<int>(sqlNextPedido, new { pedido.LkUsuario }, transaction: trans);
            }

            if (string.IsNullOrWhiteSpace(pedido.Codigo) && pedido.LkCliente.HasValue)
            {
                pedido.Codigo = pedido.LkCliente.Value.ToString("D6");
            }

            const string sqlInsertA = @"
                INSERT INTO TabEst3A (
                    Controle, Pedido, Codigo, Data, HoraIni, Cliente, LkCliente, Vendedor, LkVendedor,
                    TOTAL, DESCONTO, ACRESCIMO, SUBTOTAL, Vlpago, Entrada, Faturado, QtdItens, Status, Venda, Cancelada,
                    LkUsuario, LkCaixa, LkCxFecha, idEmpresa, Endereco, Numero, Bairro, Cidade, UF, CEP, CPF_CNPJ,
                    Telefone, eMail, ComplEnd, Obs
                ) VALUES (
                    @Controle, @Pedido, @Codigo, @Data, @HoraIni, @Cliente, @LkCliente, @Vendedor, @LkVendedor,
                    @Total, @Desconto, @Acrescimo, @Subtotal, @VlPago, @Entrada, @Faturado, @QtdItens, @Status, @Venda, @Cancelada,
                    @LkUsuario, @LkCaixa, @LkCxFecha, @IdEmpresa, @Endereco, @Numero, @Bairro, @Cidade, @UF, @CEP, @CPF_CNPJ,
                    @Telefone, @Email, @ComplEnd, @Obs
                );";

            await conn.ExecuteAsync(sqlInsertA, pedido, transaction: trans);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(pedido.Codigo) && pedido.LkCliente.HasValue)
            {
                pedido.Codigo = pedido.LkCliente.Value.ToString("D6");
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
                    Obs = @Obs
                WHERE Controle = @Controle;";

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
