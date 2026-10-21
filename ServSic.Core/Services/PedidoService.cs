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
            // Trava de Segurança Defensiva: Somente Vendas concluídas pelo caixa ('V' / Venda=1) ou Canceladas jamais podem ser alteradas
            if (pedido.Controle > 0)
            {
                var cabAtual = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Status, ISNULL(Venda, 0) as Venda, ISNULL(Cancelada, 0) as Cancelada FROM TabEst3A WHERE Controle = @Controle",
                    new { pedido.Controle }, transaction: trans);

                if (cabAtual != null)
                {
                    string statusExistente = Convert.ToString(cabAtual.Status ?? string.Empty).Trim().ToUpperInvariant();
                    bool vendaExistente = Convert.ToBoolean(cabAtual.Venda);
                    bool canceladaExistente = Convert.ToBoolean(cabAtual.Cancelada);

                    bool isConcluidaOuCancelada = vendaExistente 
                        || statusExistente == "V" 
                        || canceladaExistente 
                        || statusExistente == "C";

                    if (isConcluidaOuCancelada)
                    {
                        throw new InvalidOperationException($"O Pedido/Venda Nº {pedido.Pedido} já está concluído como venda pelo caixa ou cancelado e não pode ser alterado.");
                    }
                }
            }

            if (pedido.Pagamentos != null && pedido.Pagamentos.Count > 0)
            {
                pedido.VlPago = Math.Round(pedido.Pagamentos.Sum(p => p.Valor), 2);
                pedido.Faturado = Math.Round(pedido.Pagamentos.Where(p => p.GeraConta).Sum(p => p.Valor), 2);
                pedido.Entrada = Math.Round(pedido.Pagamentos.Where(p => !p.GeraConta).Sum(p => p.Valor), 2);
            }
            else
            {
                pedido.VlPago = 0m;
                pedido.Faturado = 0m;
                pedido.Entrada = 0m;
            }

            // Regra de Entrega: Se a venda tem dados de entrega informados (por cliente ou avulso),
            // marca automaticamente todos os itens com Entrega = 1 para a TabEst3B
            bool temEntrega = pedido.TemEnderecoEntrega 
                || (pedido.Entrega.HasValue && pedido.Entrega.Value == 1) 
                || (pedido.LkEnderecoCli.HasValue && pedido.LkEnderecoCli.Value > 0);

            if (temEntrega)
            {
                pedido.Entrega = 1;
                if (!pedido.DataEntrega.HasValue) pedido.DataEntrega = DateTime.Today;
                foreach (var it in pedido.Itens)
                {
                    it.Entrega = 1;
                    it.IsEntrega = true;
                }
            }
            else
            {
                pedido.Entrega = 0;
                foreach (var it in pedido.Itens)
                {
                    it.Entrega = 0;
                    it.IsEntrega = false;
                }
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
                pedido.IdEmpresa,
                conn,
                trans);

            // 3. Salva pagamentos na TabEst3C via PedidoPagService se houver
            if (pedido.Pagamentos != null && pedido.Pagamentos.Count > 0)
            {
                await _pagService.SalvarPagamentosAsync(pedido.Pedido, pedido.Total, pedido.Pagamentos, pedido.IdEmpresa, conn, trans);
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
    /// Exclui o pedido das tabelas TabEst3C (pagamentos), TabEst3B (itens) e TabEst3A (cabeçalho), estritamente nessa sequência.
    /// Vendas concluídas pelo caixa (Venda = 1 ou Status = 'V') jamais podem ser excluídas.
    /// </summary>
    public async Task<bool> ExcluirPedidoCompletoAsync(int controle, int numeroPedido)
    {
        using var conn = DbConnectionFactory.CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            // Trava de segurança no banco: Venda concluída jamais pode ser excluída
            var cab = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Status, Venda FROM TabEst3A WHERE Controle = @controle",
                new { controle },
                trans);

            if (cab != null)
            {
                string status = cab.Status?.ToString()?.Trim() ?? "";
                bool isVenda = Convert.ToBoolean(cab.Venda) || status == "V";
                if (isVenda)
                {
                    throw new InvalidOperationException("Vendas concluídas não podem ser excluídas.");
                }
            }

            // Exclusão sequencial obrigatória: TabEst3C -> TabEst3B -> TabEst3A
            await _pagService.ExcluirPagamentosPorPedidoAsync(numeroPedido, conn, trans);
            await _itemService.ExcluirItensPorPedidoAsync(controle, numeroPedido, conn, trans);
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
}
