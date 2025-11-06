using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tracker.Models;

namespace Tracker.Services;

public class CorreiosService
{
    private readonly HttpClient _httpClient;
    private readonly string _usuario;
    private readonly string _secretKey;
    private readonly string _cartaPostal;
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public CorreiosService(string usuario, string secretKey, string cartaPostal)
    {
        _usuario = usuario;
        _secretKey = secretKey;
        _cartaPostal = cartaPostal;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30); // Timeout de 30 segundos
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
        if (string.IsNullOrWhiteSpace(_usuario))
        {
            throw new Exception("Usuário dos Correios não configurado. Configure 'Correios:Usuario' no appsettings.json ou via variável de ambiente 'Correios__Usuario'");
        }

        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            throw new Exception("Secret Key dos Correios não configurada. Configure 'Correios:SecretKey' no appsettings.json ou via variável de ambiente 'Correios__SecretKey'");
        }

        if (string.IsNullOrWhiteSpace(_cartaPostal))
        {
            throw new Exception("Cartão Postal dos Correios não configurado. Configure 'Correios:CartaPostal' no appsettings.json");
        }

        // Obter novo token
        var authUrl = "https://api.correios.com.br/token/v1/autentica/cartaopostagem";
        
        // Criar um HttpClient separado para autenticação (sem headers anteriores)
        using var authClient = new HttpClient();
        authClient.Timeout = TimeSpan.FromSeconds(10); // Timeout de 10 segundos
        
        // Criar credenciais Basic Auth: usuario:secretKey em base64
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_usuario}:{_secretKey}"));
        
        // Criar body JSON com o número do cartão postal
        var body = new { numero = _cartaPostal };
        var jsonBody = JsonSerializer.Serialize(body);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        
        var request = new HttpRequestMessage(HttpMethod.Post, authUrl)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Log de debug (sem mostrar valores reais)
        Console.WriteLine($"      🔐 Tentando autenticar... (Usuario: {_usuario}, SecretKey: ***{(_secretKey.Length > 4 ? _secretKey.Substring(_secretKey.Length - 4) : "****")}, CartaPostal: {_cartaPostal})");
        Console.WriteLine($"      ⏳ Aguardando resposta da API (timeout: 10s)...");
        
        // Usar CancellationTokenSource para controle preciso do timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        HttpResponseMessage response;
        try
        {
            response = await authClient.SendAsync(request, cts.Token);
            Console.WriteLine($"      ✅ Resposta recebida: {response.StatusCode}");
        }
        catch (TaskCanceledException) when (cts.Token.IsCancellationRequested)
        {
            throw new Exception("Timeout ao tentar autenticar nos Correios. A API não respondeu em 10 segundos. Verifique:\n" +
                "  - Conectividade de rede\n" +
                "  - Se a API dos Correios está acessível\n" +
                "  - Se há firewall bloqueando a conexão\n" +
                "  - Teste manualmente: curl -X POST https://api.correios.com.br/token/v1/autentica/cartaopostagem");
        }
        catch (TaskCanceledException ex)
        {
            throw new Exception($"Requisição cancelada: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Erro de rede ao tentar autenticar nos Correios: {ex.Message}\n" +
                "Verifique a conectividade de rede e se a API dos Correios está acessível.");
        }
        
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
                    $"  - Se o usuário dos Correios está correto\n" +
                    $"  - Se a secret key está correta\n" +
                    $"  - Se o cartão postal está correto\n" +
                    $"  - Se as credenciais têm permissão para acessar a API\n" +
                    $"  - Se as variáveis de ambiente Correios__Usuario e Correios__SecretKey estão configuradas corretamente");
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

