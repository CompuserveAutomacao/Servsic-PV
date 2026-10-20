using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço responsável pela persistência das configurações de impressão térmica
/// e obtenção dos dados cadastrais da empresa para o cabeçalho do cupom.
/// </summary>
public class ImpressoraConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ServsicPV",
        "printer_config.json"
    );

    private static ConfiguracaoImpressora? _cachedConfig;
    private static DadosEmpresa? _cachedEmpresa;

    public ConfiguracaoImpressora ObterConfiguracao()
    {
        if (_cachedConfig != null) return _cachedConfig;

        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<ConfiguracaoImpressora>(json);
                if (config != null)
                {
                    if (config.LarguraColunas <= 0)
                    {
                        config.LarguraColunas = 42;
                    }

                    _cachedConfig = config;
                    return _cachedConfig;
                }
            }
        }
        catch
        {
            // Fallback para padrão se arquivo corrompido
        }

        _cachedConfig = new ConfiguracaoImpressora();
        return _cachedConfig;
    }

    public void SalvarConfiguracao(ConfiguracaoImpressora config)
    {
        try
        {
            string dir = Path.GetDirectoryName(ConfigPath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigPath, json);
            _cachedConfig = config;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImpressoraConfigService] Erro ao salvar configuração: {ex.Message}");
        }
    }

    public async Task<DadosEmpresa> ObterDadosEmpresaAsync()
    {
        if (_cachedEmpresa != null) return _cachedEmpresa;

        try
        {
            using var conn = DbConnectionFactory.CreateConnection();
            
            // Tenta obter de CFG_EMPRESA
            const string sqlCfg = @"
                SELECT TOP 1
                    ISNULL(NOME, RAZAOSOCIAL) as NomeFantasia,
                    ISNULL(RAZAOSOCIAL, NOME) as RazaoSocial,
                    ISNULL(CGC, '') as Cnpj,
                    ISNULL(INSC, '') as InscricaoEstadual,
                    ISNULL(ENDERECO, '') as Endereco,
                    ISNULL(Numero, '') as Numero,
                    ISNULL(Bairro, '') as Bairro,
                    ISNULL(CIDADE, '') as Cidade,
                    ISNULL(UF, '') as UF,
                    ISNULL(CEP, '') as CEP,
                    ISNULL(TELEFONE, '') as Telefone
                FROM CFG_EMPRESA";

            var empresa = await conn.QueryFirstOrDefaultAsync<DadosEmpresa>(sqlCfg);
            if (empresa != null && !string.IsNullOrWhiteSpace(empresa.NomeFantasia))
            {
                _cachedEmpresa = empresa;
                return _cachedEmpresa;
            }

            // Fallback para TabEmpre
            const string sqlEmpre = @"
                SELECT TOP 1
                    ISNULL(FANTASIA, Empresa) as NomeFantasia,
                    ISNULL(Empresa, FANTASIA) as RazaoSocial,
                    ISNULL(CGC, '') as Cnpj,
                    ISNULL(INSC, '') as InscricaoEstadual,
                    ISNULL(Endereco, '') as Endereco,
                    ISNULL(Bairro, '') as Bairro,
                    ISNULL(Cidade, '') as Cidade,
                    ISNULL(Estado, '') as UF,
                    ISNULL(CEP, '') as CEP,
                    ISNULL(Telefone, '') as Telefone
                FROM TabEmpre";

            var empre = await conn.QueryFirstOrDefaultAsync<DadosEmpresa>(sqlEmpre);
            if (empre != null)
            {
                _cachedEmpresa = empre;
                return _cachedEmpresa;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImpressoraConfigService] Erro ao carregar dados da empresa: {ex.Message}");
        }

        _cachedEmpresa = new DadosEmpresa();
        return _cachedEmpresa;
    }
}
