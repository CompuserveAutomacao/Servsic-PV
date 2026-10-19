using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ServSic.Core.Services;

public class EnderecoCepResult
{
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Localidade { get; set; }
    public string? Uf { get; set; }
    public int? Ibge { get; set; }
    public bool Erro { get; set; }
}

public class CepLookupService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<EnderecoCepResult?> ConsultarAsync(string cep)
    {
        if (string.IsNullOrWhiteSpace(cep)) return null;

        string digits = Regex.Replace(cep, @"\D", "");
        if (digits.Length != 8) return null;

        try
        {
            string url = $"https://viacep.com.br/ws/{digits}/json/";
            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("erro", out var erroProp))
            {
                if (erroProp.ValueKind == JsonValueKind.True || (erroProp.ValueKind == JsonValueKind.String && erroProp.GetString() == "true"))
                {
                    return new EnderecoCepResult { Erro = true };
                }
            }

            int? ibge = null;
            if (root.TryGetProperty("ibge", out var ibgeProp) && int.TryParse(ibgeProp.GetString(), out int ibgeVal))
            {
                ibge = ibgeVal;
            }

            return new EnderecoCepResult
            {
                Cep = root.TryGetProperty("cep", out var c) ? c.GetString() : digits,
                Logradouro = root.TryGetProperty("logradouro", out var l) ? l.GetString() : null,
                Complemento = root.TryGetProperty("complemento", out var comp) ? comp.GetString() : null,
                Bairro = root.TryGetProperty("bairro", out var b) ? b.GetString() : null,
                Localidade = root.TryGetProperty("localidade", out var loc) ? loc.GetString() : null,
                Uf = root.TryGetProperty("uf", out var u) ? u.GetString() : null,
                Ibge = ibge,
                Erro = false
            };
        }
        catch
        {
            return null;
        }
    }
}
