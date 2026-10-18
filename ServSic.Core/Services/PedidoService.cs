using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Orquestrador do fluxo completo de pedidos, coordenando os serviços especializados:
/// - PedidoCabService (Cabeçalho - TabEst3A)
/// - PedidoItemService (Itens - TabEst3B)
/// - PedidoPagService (Pagamentos - TabEst3C)
/// </summary>
public class PedidoService
{
    private readonly PedidoCabService _cabService;
    private readonly PedidoItemService _itemService;
    private readonly PedidoPagService _pagService;

    public PedidoService(
        PedidoCabService? cabService = null,
        PedidoItemService? itemService = null,
        PedidoPagService? pagService = null)
    {
        _cabService = cabService ?? new PedidoCabService();
        _itemService = itemService ?? new PedidoItemService();
        _pagService = pagService ?? new PedidoPagService();
    }

    public async Task<PedidoVenda?> ObterPorIdAsync(int controle)
    {
        var pedido = await _cabService.ObterPorIdAsync(controle);
        if (pedido == null) return null;

        var itens = await _itemService.ObterItensPorPedidoAsync(controle);
        pedido.Itens = new System.Collections.ObjectModel.ObservableCollection<PedidoItem>(itens);

        var pagamentos = await _pagService.ObterPagamentosPorPedidoAsync(pedido.Pedido);
        pedido.Pagamentos = pagamentos.ToList();

        return pedido;
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
        return await _cabService.ListarAsync(status, search, dataInicio, dataFim, limite, lkUsuario, lkVendedor);
    }

    public async Task<PedidoVenda> SalvarAsync(PedidoVenda pedido)
    {
        // Venda Balcão / Orçamento: O cliente não é obrigatório para fechamento rápido
        if (string.IsNullOrWhiteSpace(pedido.Cliente))
        {
            pedido.Cliente = null;
            pedido.LkCliente = null;
            pedido.Codigo = null;
        }

        if (pedido.Itens == null || pedido.Itens.Count == 0)
            throw new InvalidOperationException("O pedido/orçamento deve conter pelo menos um item.");

        // O Core recalcula tudo para assegurar integridade matemática absoluta
        pedido.RecalcularTotais();

        using var conn = DbConnectionFactory.CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            if (pedido.Pagamentos != null && pedido.Pagamentos.Count > 0)
            {
                pedido.VlPago = pedido.Pagamentos.Sum(p => p.Valor);
                pedido.Faturado = pedido.Pagamentos.Where(p => p.GeraConta).Sum(p => p.Valor);
            }

            // 1. Salva cabeçalho na TabEst3A via PedidoCabService
            pedido.Controle = await _cabService.SalvarCabecalhoAsync(pedido, conn, trans);

            // 2. Salva itens na TabEst3B com LkVendedor via PedidoItemService
            await _itemService.SalvarItensAsync(
                pedido.Controle,
                pedido.Pedido,
                pedido.Itens,
                pedido.LkVendedor,
                pedido.Vendedor,
                conn,
                trans);

            // 3. Salva pagamentos na TabEst3C via PedidoPagService se houver
            if (pedido.Pagamentos != null && pedido.Pagamentos.Count > 0)
            {
                await _pagService.SalvarPagamentosAsync(pedido.Pedido, pedido.Total, pedido.Pagamentos, conn, trans);
            }

            trans.Commit();
            return pedido;
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task<bool> ConverterOrcamentoEmVendaAsync(int controle)
    {
        return await _cabService.ConverterEmVendaAsync(controle);
    }

    public async Task<bool> CancelarPedidoAsync(int controle)
    {
        return await _cabService.CancelarAsync(controle);
    }

    /// <summary>
    /// Exclui o pedido das tabelas TabEst3A (cabeçalho), TabEst3B (itens) e TabEst3C (pagamentos).
    /// </summary>
    public async Task<bool> ExcluirPedidoCompletoAsync(int controle, int numeroPedido)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            await _pagService.ExcluirPagamentosPorPedidoAsync(numeroPedido, conn, trans);
            await _itemService.ExcluirItensPorPedidoAsync(controle, conn, trans);
            await conn.ExecuteAsync("DELETE FROM TabEst3A WHERE Controle = @controle", new { controle }, trans);
            trans.Commit();
            return true;
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Exclui todos os orçamentos e pré-vendas pendentes vinculados ao operador logado.
    /// </summary>
    public async Task<int> ExcluirPendentesAsync(int? lkUsuario, int? lkVendedor)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            const string sqlBusca = @"
                SELECT Controle, Codigo FROM TabEst3A 
                WHERE Status IN ('O', 'P') AND Cancelada = 0
                AND ((@lkUsuario IS NULL OR LkUsuario = @lkUsuario) OR (@lkVendedor IS NULL OR LkVendedor = @lkVendedor))";

            var pendentes = (await conn.QueryAsync<(int Controle, int Codigo)>(sqlBusca, new { lkUsuario, lkVendedor }, trans)).ToList();
            foreach (var p in pendentes)
            {
                await _pagService.ExcluirPagamentosPorPedidoAsync(p.Codigo, conn, trans);
                await _itemService.ExcluirItensPorPedidoAsync(p.Controle, conn, trans);
                await conn.ExecuteAsync("DELETE FROM TabEst3A WHERE Controle = @controle", new { controle = p.Controle }, trans);
            }

            trans.Commit();
            return pendentes.Count;
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }
}
