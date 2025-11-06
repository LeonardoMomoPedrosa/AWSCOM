using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tracker.Models;

namespace Tracker.Services;

public class CorreiosService
{
    private readonly HttpClient _httpClient;
    private readonly string _key;
    private readonly string _cartaPostal;
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public CorreiosService(string key, string cartaPostal)
    {
        _key = key;
        _cartaPostal = cartaPostal;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<CorreiosRastreamentoDTO> GetRastreamentoAsync(string codigoRastreamento)
    {
        var token = await GetTokenAsync();
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var url = $"https://api.correios.com.br/srorastro/v1/objetos/{codigoRastreamento}?resultado=T";
        var response = await _httpClient.GetAsync(url);
        
        response.EnsureSuccessStatusCode();
        
        var jsonContent = await response.Content.ReadAsStringAsync();
        var rastreamento = JsonSerializer.Deserialize<CorreiosRastreamentoDTO>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (rastreamento == null)
        {
            throw new Exception("Resposta da API dos Correios não pôde ser deserializada");
        }

        return rastreamento;
    }

    private async Task<string> GetTokenAsync()
    {
        // Verificar se o token está em cache e ainda válido (válido por 1 hora)
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }

        // Obter novo token
        var authUrl = "https://api.correios.com.br/token/v1/autentica/cartaopostagem";
        
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_key}:{_cartaPostal}"));
        
        var request = new HttpRequestMessage(HttpMethod.Post, authUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var jsonContent = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<CorreiosAuthDTO>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (authResponse?.Token == null)
        {
            throw new Exception("Não foi possível obter token de autenticação dos Correios");
        }

        _cachedToken = authResponse.Token;
        _tokenExpiry = DateTime.UtcNow.AddHours(1); // Token válido por 1 hora

        return _cachedToken;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

