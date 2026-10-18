using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ServSic.Core.Services;

public class CnpjResult
{
    public bool Sucesso { get; set; }
    public string? MensagemErro { get; set; }
    public string? Cnpj { get; set; }
    public string? RazaoSocial { get; set; }
    public string? NomeFantasia { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string? Municipio { get; set; }
    public string? Uf { get; set; }
}

public class CnpjLookupService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    static CnpjLookupService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ServsicPV/1.0");
    }

    public async Task<CnpjResult> ConsultarAsync(string cnpj)
    {
        string digits = Regex.Replace(cnpj ?? string.Empty, @"\D", "");
        if (digits.Length != 14)
        {
            return new CnpjResult { Sucesso = false, MensagemErro = "O CNPJ deve conter 14 dígitos numéricos." };
        }

        // 1. Tenta BrasilAPI
        try
        {
            string url = $"https://brasilapi.com.br/api/cnpj/v1/{digits}";
            using var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var res = new CnpjResult
                {
                    Sucesso = true,
                    Cnpj = digits,
                    RazaoSocial = GetString(root, "razao_social"),
                    NomeFantasia = GetString(root, "nome_fantasia"),
                    Cep = GetString(root, "cep"),
                    Logradouro = GetString(root, "logradouro"),
                    Numero = GetString(root, "numero"),
                    Bairro = GetString(root, "bairro"),
                    Municipio = GetString(root, "municipio"),
                    Uf = GetString(root, "uf"),
                    Email = GetString(root, "email")
                };

                string tel1 = GetString(root, "ddd_telefone_1");
                string tel2 = GetString(root, "ddd_telefone_2");
                res.Telefone = !string.IsNullOrWhiteSpace(tel1) ? tel1 : tel2;

                return res;
            }
        }
        catch { /* Fallback para ReceitaWS */ }

        // 2. Fallback ReceitaWS
        try
        {
            string url = $"https://receitaws.com.br/v1/cnpj/{digits}";
            using var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var st) && st.GetString() == "ERROR")
                {
                    string msg = GetString(root, "message") ?? "CNPJ não localizado.";
                    return new CnpjResult { Sucesso = false, MensagemErro = msg };
                }

                return new CnpjResult
                {
                    Sucesso = true,
                    Cnpj = digits,
                    RazaoSocial = GetString(root, "nome"),
                    NomeFantasia = GetString(root, "fantasia"),
                    Cep = GetString(root, "cep"),
                    Logradouro = GetString(root, "logradouro"),
                    Numero = GetString(root, "numero"),
                    Bairro = GetString(root, "bairro"),
                    Municipio = GetString(root, "municipio"),
                    Uf = GetString(root, "uf"),
                    Telefone = GetString(root, "telefone"),
                    Email = GetString(root, "email")
                };
            }
        }
        catch (Exception ex)
        {
            return new CnpjResult { Sucesso = false, MensagemErro = $"Erro na consulta online: {ex.Message}" };
        }

        return new CnpjResult { Sucesso = false, MensagemErro = "Não foi possível obter dados do CNPJ nos serviços públicos." };
    }

    private static string? GetString(JsonElement element, string propName)
    {
        if (element.TryGetProperty(propName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetRawText();
        }
        return null;
    }
}
