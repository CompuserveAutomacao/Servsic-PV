using System;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

public class ConfiguracaoVendaService
{
    private ConfiguracaoVenda? _cachedConfig;
    private DateTime _lastLoadTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<ConfiguracaoVenda> ObterConfiguracaoAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedConfig != null && (DateTime.UtcNow - _lastLoadTime) < CacheDuration)
        {
            return _cachedConfig;
        }

        var config = new ConfiguracaoVenda();

        try
        {
            using var conn = DbConnectionFactory.CreateConnection();

            // 1. CFG_ORCAMENTO: ESTOQUEINSUFICIENTE ('S' / 'N')
            try
            {
                var orcEst = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT TOP 1 ESTOQUEINSUFICIENTE FROM CFG_ORCAMENTO WHERE ESTOQUEINSUFICIENTE IS NOT NULL").ConfigureAwait(false);
                config.HabilitaOrcamentoComEstoqueInsuficiente = string.Equals(orcEst?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            // 2. CFG_PREVENDA: ESTOQUEINSUFICIENTE ('S' / 'N')
            try
            {
                var prevEst = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT TOP 1 ESTOQUEINSUFICIENTE FROM CFG_PREVENDA WHERE ESTOQUEINSUFICIENTE IS NOT NULL").ConfigureAwait(false);
                config.HabilitaPreVendaComEstoqueInsuficiente = string.Equals(prevEst?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            // 3. CFG_VENDA: ESTOQUEINSUFICIENTE, BloqueiaTotalizacaoEstoqueInsuficiente, PRECOMENOR
            try
            {
                var vendaRow = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT TOP 1 
                        ESTOQUEINSUFICIENTE, 
                        BloqueiaTotalizacaoEstoqueInsuficiente, 
                        PRECOMENOR 
                      FROM CFG_VENDA").ConfigureAwait(false);

                if (vendaRow != null)
                {
                    string? vEst = vendaRow.ESTOQUEINSUFICIENTE?.ToString();
                    string? vBloq = vendaRow.BloqueiaTotalizacaoEstoqueInsuficiente?.ToString();
                    string? vPrec = vendaRow.PRECOMENOR?.ToString();

                    config.HabilitaVendaComEstoqueInsuficiente = string.Equals(vEst?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
                    config.BloqueiaTotalizacaoEstoqueInsuficiente = string.Equals(vBloq?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
                    config.HabilitaPrecoMenorQueTabela = string.Equals(vPrec?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }

            _cachedConfig = config;
            _lastLoadTime = DateTime.UtcNow;
            return config;
        }
        catch
        {
            return _cachedConfig ?? config;
        }
    }

    /// <summary>
    /// Retorna se o tipo de operação atual permite movimentar itens com estoque insuficiente (Assíncrono).
    /// </summary>
    public async Task<bool> PermiteEstoqueInsuficienteAsync(bool isOrcamento)
    {
        var cfg = await ObterConfiguracaoAsync().ConfigureAwait(false);
        return isOrcamento
            ? cfg.HabilitaOrcamentoComEstoqueInsuficiente
            : cfg.HabilitaPreVendaComEstoqueInsuficiente;
    }

    /// <summary>
    /// Retorna imediatamente da memória se permite estoque insuficiente, sem risco de deadlock na UI.
    /// Caso ainda não esteja em cache, dispara carga em background e retorna true por segurança.
    /// </summary>
    public bool PermiteEstoqueInsuficiente(bool isOrcamento)
    {
        if (_cachedConfig == null)
        {
            _ = Task.Run(async () => await ObterConfiguracaoAsync().ConfigureAwait(false));
            return true;
        }

        return isOrcamento
            ? _cachedConfig.HabilitaOrcamentoComEstoqueInsuficiente
            : _cachedConfig.HabilitaPreVendaComEstoqueInsuficiente;
    }
}
