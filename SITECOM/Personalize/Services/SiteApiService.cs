using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Personalize.Models;

namespace Personalize.Services;

public class SiteApiService : ISiteApiService, IDisposable
{
    private readonly SiteApiConfig _config;

    public SiteApiService(SiteApiConfig config)
    {
        _config = config;
    }

    public async Task<bool> InvalidateAsync(CacheInvalidateRequest request)
    {
        return await InvalidateAsync(new[] { request });
    }

    public async Task<bool> InvalidateAsync(IEnumerable<CacheInvalidateRequest> requests)
    {
        var requestsList = requests.ToList();
        if (requestsList.Count == 0)
        {
            return true;
        }

        try
        {
            var token = await GetTokenAsync();
            var allSuccess = true;

            var tasks = _config.Servers.Select(async server =>
            {
                try
                {
                    var httpClient = new HttpClient();
                    httpClient.BaseAddress = new Uri(server.BaseUrl);
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", token);
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                    var body = JsonSerializer.Serialize(requestsList);
                    var content = new StringContent(body, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync(_config.InvalidateApi, content);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"   ⚠️  Falha ao invalidar cache no servidor {server.BaseUrl}: {(int)response.StatusCode} {errorContent}");
                        allSuccess = false;
                    }
                    else
                    {
                        Console.WriteLine($"   ✅ Cache invalidado no servidor {server.BaseUrl}");
                    }

                    httpClient.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Erro ao invalidar cache no servidor {server.BaseUrl}: {ex.Message}");
                    allSuccess = false;
                }
            });

            await Task.WhenAll(tasks);
            return allSuccess;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erro ao obter token para invalidação de cache: {ex.Message}");
            return false;
        }
    }

    private async Task<string> GetTokenAsync()
    {
        if (_config.Servers.Count == 0)
        {
            throw new Exception("Nenhum servidor configurado em SiteApi.Servers");
        }

        var server = _config.Servers.First();
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(server.BaseUrl);
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var credentials = new 
        { 
            username = _config.Username, 
            password = _config.Password 
        };
        
        var payload = JsonSerializer.Serialize(credentials);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(_config.AuthPath, content);
        var respStr = await response.Content.ReadAsStringAsync();

        httpClient.Dispose();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Auth failed: {(int)response.StatusCode} {respStr}");
        }

        var auth = JsonSerializer.Deserialize<CacheAuthResponse>(respStr);
        if (auth == null || string.IsNullOrEmpty(auth.Token))
        {
            throw new Exception("Token não encontrado na resposta de autenticação");
        }

        return auth.Token;
    }

    public void Dispose()
    {
        // Nada a fazer - HttpClient é criado localmente e já é descartado
    }
}

