using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

public class LiberacaoService
{
    public async Task<int> CriarSolicitacaoAsync(SolicitacaoLiberacao sol)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO dbo.TabSolLiberacao (
                DataHora, Terminal, LkUsuario, NomeUsuario, LkVendedor, NomeVendedor,
                LkCliente, NomeCliente, LkPedido, Tipo, Descricao, Detalhes,
                ValorOriginal, ValorSolicitado, PercentualDesconto,
                LkProduto, QtdSolicitada, EstoqueAtual, Status
            ) VALUES (
                GETDATE(), @Terminal, @LkUsuario, @NomeUsuario, @LkVendedor, @NomeVendedor,
                @LkCliente, @NomeCliente, @LkPedido, @Tipo, @Descricao, @Detalhes,
                @ValorOriginal, @ValorSolicitado, @PercentualDesconto,
                @LkProduto, @QtdSolicitada, @EstoqueAtual, 'P'
            );
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await conn.ExecuteScalarAsync<int>(sql, sol);
    }

    public async Task VincularPedidoAsync(IEnumerable<int> idsSolicitacoes, int numPedido)
    {
        if (idsSolicitacoes == null || !idsSolicitacoes.Any() || numPedido <= 0) return;
        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            const string sql = @"
                UPDATE dbo.TabSolLiberacao
                SET LkPedido = @NumPedido
                WHERE Controle IN @Ids AND (LkPedido IS NULL OR LkPedido = 0);";
            await conn.ExecuteAsync(sql, new { NumPedido = numPedido, Ids = idsSolicitacoes });
        }
        catch
        {
            // Tratamento silencioso para não impactar a finalização da venda
        }
    }

    public async Task<SolicitacaoLiberacao?> ObterPorIdAsync(int controle)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.TabSolLiberacao WITH (NOLOCK)
            WHERE Controle = @Controle;";

        return await conn.QueryFirstOrDefaultAsync<SolicitacaoLiberacao>(sql, new { Controle = controle });
    }

    public async Task<bool> CancelarSolicitacaoAsync(int controle)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.TabSolLiberacao
            SET Status = 'C', DataHoraResposta = GETDATE()
            WHERE Controle = @Controle AND Status = 'P';";

        int rows = await conn.ExecuteAsync(sql, new { Controle = controle });
        return rows > 0;
    }

    public async Task<List<SolicitacaoLiberacao>> ListarPendentesAsync()
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.TabSolLiberacao WITH (NOLOCK)
            WHERE Status = 'P'
            ORDER BY DataHora ASC;";

        var itens = await conn.QueryAsync<SolicitacaoLiberacao>(sql);
        return itens.ToList();
    }

    public async Task<List<SolicitacaoLiberacao>> ListarHistoricoHojeAsync()
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP 100 * FROM dbo.TabSolLiberacao WITH (NOLOCK)
            WHERE CAST(DataHora AS DATE) = CAST(GETDATE() AS DATE)
              AND Status IN ('A', 'R', 'C')
            ORDER BY DataHoraResposta DESC, DataHora DESC;";

        var itens = await conn.QueryAsync<SolicitacaoLiberacao>(sql);
        return itens.ToList();
    }

    public async Task<int> ContarPendentesAsync()
    {
        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            const string sql = @"
                SELECT COUNT(1) FROM dbo.TabSolLiberacao WITH (NOLOCK)
                WHERE Status = 'P';";

            return await conn.ExecuteScalarAsync<int>(sql);
        }
        catch
        {
            return 0;
        }
    }

    public async Task<bool> AprovarSolicitacaoAsync(int controle, int lkSupervisor, string nomeSupervisor)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.TabSolLiberacao
            SET Status = 'A',
                LkSupervisor = @LkSupervisor,
                NomeSupervisor = @NomeSupervisor,
                DataHoraResposta = GETDATE()
            WHERE Controle = @Controle AND Status = 'P';";

        int rows = await conn.ExecuteAsync(sql, new
        {
            Controle = controle,
            LkSupervisor = lkSupervisor,
            NomeSupervisor = nomeSupervisor
        });

        return rows > 0;
    }

    public async Task<bool> RecusarSolicitacaoAsync(int controle, int lkSupervisor, string nomeSupervisor, string motivoRecusa)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE dbo.TabSolLiberacao
            SET Status = 'R',
                LkSupervisor = @LkSupervisor,
                NomeSupervisor = @NomeSupervisor,
                DataHoraResposta = GETDATE(),
                MotivoRecusa = @MotivoRecusa
            WHERE Controle = @Controle AND Status = 'P';";

        int rows = await conn.ExecuteAsync(sql, new
        {
            Controle = controle,
            LkSupervisor = lkSupervisor,
            NomeSupervisor = nomeSupervisor,
            MotivoRecusa = motivoRecusa
        });

        return rows > 0;
    }
}
