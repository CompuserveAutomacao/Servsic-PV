using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        pedido.Itens = itens.ToList();

        var pagamentos = await _pagService.ObterPagamentosPorPedidoAsync(pedido.Pedido);
        pedido.Pagamentos = pagamentos.ToList();

        return pedido;
    }

    public async Task<IReadOnlyList<PedidoVenda>> ListarAsync(
        string? status = null,
        string? search = null,
        DateTime? dataInicio = null,
        DateTime? dataFim = null,
        int limite = 100)
    {
        return await _cabService.ListarAsync(status, search, dataInicio, dataFim, limite);
    }

    public async Task<PedidoVenda> SalvarAsync(PedidoVenda pedido)
    {
        if (string.IsNullOrWhiteSpace(pedido.Cliente))
            throw new InvalidOperationException("O nome do cliente é obrigatório para registrar o pedido/orçamento.");

        if (pedido.Itens == null || pedido.Itens.Count == 0)
            throw new InvalidOperationException("O pedido/orçamento deve conter pelo menos um item.");

        // O Core recalcula tudo para assegurar integridade matemática absoluta
        pedido.RecalcularTotais();

        using var conn = DbConnectionFactory.CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
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
                await _pagService.SalvarPagamentosAsync(pedido.Pedido, pedido.Pagamentos, conn, trans);
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
}
