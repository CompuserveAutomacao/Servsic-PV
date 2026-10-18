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
        int limite = 100)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("limite", limite);

        var filters = new List<string> { "1=1" };

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add("ta.Status = @status");
            parameters.Add("status", status.Trim());
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
                    BEGIN TRY
                        EXEC NumeroPedido @NumPed OUTPUT;
                    END TRY
                    BEGIN CATCH
                        SELECT @NumPed = ISNULL(MAX(Pedido), 0) + 1 FROM TabEst3A;
                    END CATCH
                    SELECT @NumPed;";
                pedido.Pedido = await conn.ExecuteScalarAsync<int>(sqlNextPedido, transaction: trans);
            }

            const string sqlInsertA = @"
                INSERT INTO TabEst3A (
                    Controle, Pedido, Data, HoraIni, Cliente, LkCliente, Vendedor, LkVendedor,
                    TOTAL, DESCONTO, ACRESCIMO, SUBTOTAL, Vlpago, QtdItens, Status, Venda, Cancelada,
                    LkUsuario, idEmpresa, Endereco, Numero, Bairro, Cidade, UF, CEP, CPF_CNPJ,
                    Telefone, eMail, ComplEnd, Obs
                ) VALUES (
                    @Controle, @Pedido, @Data, @HoraIni, @Cliente, @LkCliente, @Vendedor, @LkVendedor,
                    @Total, @Desconto, @Acrescimo, @Subtotal, @VlPago, @QtdItens, @Status, @Venda, @Cancelada,
                    @LkUsuario, @IdEmpresa, @Endereco, @Numero, @Bairro, @Cidade, @UF, @CEP, @CPF_CNPJ,
                    @Telefone, @Email, @ComplEnd, @Obs
                );";

            await conn.ExecuteAsync(sqlInsertA, pedido, transaction: trans);
        }
        else
        {
            const string sqlUpdateA = @"
                UPDATE TabEst3A SET
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
                    QtdItens = @QtdItens,
                    Status = @Status,
                    Venda = @Venda,
                    Cancelada = @Cancelada,
                    LkUsuario = @LkUsuario,
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
                Status = 'V',
                Venda = 1
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
