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
        if (string.IsNullOrWhiteSpace(codigoRastreamento))
        {
            throw new ArgumentException("Código de rastreamento não pode ser vazio", nameof(codigoRastreamento));
        }

        var token = await GetTokenAsync();
        
        // Limpar header anterior e adicionar novo token
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var url = $"https://api.correios.com.br/srorastro/v1/objetos/{codigoRastreamento}?resultado=T";
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erro ao consultar rastreamento dos Correios: {response.StatusCode} - {errorContent}. Código: {codigoRastreamento}");
        }
        
        var jsonContent = await response.Content.ReadAsStringAsync();
        var rastreamento = JsonSerializer.Deserialize<CorreiosRastreamentoDTO>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (rastreamento == null)
        {
            throw new Exception($"Resposta da API dos Correios não pôde ser deserializada. Resposta: {jsonContent}");
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

        // Validar credenciais
        if (string.IsNullOrWhiteSpace(_key))
        {
            throw new Exception("Chave dos Correios não configurada. Configure 'Correios:Key' no appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(_cartaPostal))
        {
            throw new Exception("Cartão Postal dos Correios não configurado. Configure 'Correios:CartaPostal' no appsettings.json");
        }

        // Obter novo token
        var authUrl = "https://api.correios.com.br/token/v1/autentica/cartaopostagem";
        
        // Criar um HttpClient separado para autenticação (sem headers anteriores)
        using var authClient = new HttpClient();
        
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_key}:{_cartaPostal}"));
        
        var request = new HttpRequestMessage(HttpMethod.Post, authUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Log de debug (sem mostrar valores reais)
        Console.WriteLine($"      🔐 Tentando autenticar... (Key: {(_key.Length > 0 ? "***" + _key.Substring(Math.Max(0, _key.Length - 4)) : "VAZIA")}, CartaPostal: {_cartaPostal})");
        
        var response = await authClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCode = response.StatusCode;
            
            // Log detalhado do erro
            Console.WriteLine($"      ❌ Status: {statusCode}");
            Console.WriteLine($"      ❌ Resposta: {errorContent}");
            
            if (statusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exception($"Autenticação falhou (401). Verifique:\n" +
                    $"  - Se a chave dos Correios está correta\n" +
                    $"  - Se o cartão postal está correto\n" +
                    $"  - Se as credenciais têm permissão para acessar a API\n" +
                    $"  - Se a variável de ambiente Correios__Key está configurada corretamente");
            }
            
            throw new Exception($"Erro ao obter token dos Correios: {statusCode} - {errorContent}");
        }
        
        var jsonContent = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<CorreiosAuthDTO>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (authResponse?.Token == null)
        {
            throw new Exception($"Não foi possível obter token de autenticação dos Correios. Resposta: {jsonContent}");
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

