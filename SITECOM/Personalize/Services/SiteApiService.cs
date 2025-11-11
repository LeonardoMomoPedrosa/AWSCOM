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
                HttpClient? httpClient = null;
                try
                {
                    var fullUrl = new Uri(new Uri(server.BaseUrl), _config.InvalidateApi).ToString();
                    Console.WriteLine($"   🔄 Tentando invalidar cache no servidor: {server.BaseUrl}");
                    Console.WriteLine($"   📍 URL completa: {fullUrl}");
                    Console.WriteLine($"   📦 Requisições a invalidar: {requestsList.Count}");

                    httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    httpClient.BaseAddress = new Uri(server.BaseUrl);
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", token);
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                    var body = JsonSerializer.Serialize(requestsList);
                    var content = new StringContent(body, Encoding.UTF8, "application/json");

                    Console.WriteLine($"   📤 Enviando requisição POST...");
                    var response = await httpClient.PostAsync(_config.InvalidateApi, content);
                    
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"   ⚠️  FALHA ao invalidar cache no servidor {server.BaseUrl}");
                        Console.WriteLine($"   📊 Status Code: {(int)response.StatusCode} ({response.StatusCode})");
                        Console.WriteLine($"   📋 Reason Phrase: {response.ReasonPhrase}");
                        Console.WriteLine($"   📄 Response Body: {responseContent}");
                        Console.WriteLine($"   🔗 URL: {fullUrl}");
                        Console.WriteLine($"   📦 Request Body (primeiros 500 chars): {(body.Length > 500 ? body.Substring(0, 500) + "..." : body)}");
                        allSuccess = false;
                    }
                    else
                    {
                        Console.WriteLine($"   ✅ Cache invalidado no servidor {server.BaseUrl}");
                        Console.WriteLine($"   📊 Status Code: {(int)response.StatusCode}");
                        if (!string.IsNullOrWhiteSpace(responseContent))
                        {
                            Console.WriteLine($"   📄 Response: {responseContent}");
                        }
                    }

                    httpClient.Dispose();
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    Console.WriteLine($"   ❌ TIMEOUT ao invalidar cache no servidor {server.BaseUrl}");
                    Console.WriteLine($"   ⏱️  Timeout após 30 segundos");
                    Console.WriteLine($"   🔗 URL: {new Uri(new Uri(server.BaseUrl), _config.InvalidateApi)}");
                    Console.WriteLine($"   📋 Exception Type: {ex.GetType().Name}");
                    Console.WriteLine($"   📄 Message: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"   📄 Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"   📚 Stack Trace:\n{ex.StackTrace}");
                    allSuccess = false;
                    httpClient?.Dispose();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"   ❌ ERRO HTTP ao invalidar cache no servidor {server.BaseUrl}");
                    Console.WriteLine($"   🔗 URL: {new Uri(new Uri(server.BaseUrl), _config.InvalidateApi)}");
                    Console.WriteLine($"   📋 Exception Type: {ex.GetType().Name}");
                    Console.WriteLine($"   📄 Message: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"   📄 Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                        if (ex.InnerException.StackTrace != null)
                        {
                            Console.WriteLine($"   📚 Inner Stack Trace:\n{ex.InnerException.StackTrace}");
                        }
                    }
                    Console.WriteLine($"   📚 Stack Trace:\n{ex.StackTrace}");
                    allSuccess = false;
                    httpClient?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ ERRO ao invalidar cache no servidor {server.BaseUrl}");
                    Console.WriteLine($"   🔗 URL: {new Uri(new Uri(server.BaseUrl), _config.InvalidateApi)}");
                    Console.WriteLine($"   📋 Exception Type: {ex.GetType().Name}");
                    Console.WriteLine($"   📄 Message: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"   📄 Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                        if (ex.InnerException.StackTrace != null)
                        {
                            Console.WriteLine($"   📚 Inner Stack Trace:\n{ex.InnerException.StackTrace}");
                        }
                    }
                    Console.WriteLine($"   📚 Stack Trace:\n{ex.StackTrace}");
                    allSuccess = false;
                    httpClient?.Dispose();
                }
            });

            await Task.WhenAll(tasks);
            return allSuccess;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ ERRO CRÍTICO ao processar invalidação de cache");
            Console.WriteLine($"   📋 Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"   📄 Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   📄 Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                if (ex.InnerException.StackTrace != null)
                {
                    Console.WriteLine($"   📚 Inner Stack Trace:\n{ex.InnerException.StackTrace}");
                }
            }
            Console.WriteLine($"   📚 Stack Trace:\n{ex.StackTrace}");
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
        var fullUrl = new Uri(new Uri(server.BaseUrl), _config.AuthPath).ToString();
        HttpClient? httpClient = null;
        
        try
        {
            Console.WriteLine($"   🔐 Autenticando no servidor: {server.BaseUrl}");
            Console.WriteLine($"   📍 URL de autenticação: {fullUrl}");
            Console.WriteLine($"   👤 Username: {_config.Username}");
            
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.BaseAddress = new Uri(server.BaseUrl);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var credentials = new 
            { 
                username = _config.Username, 
                password = _config.Password 
            };
            
            var payload = JsonSerializer.Serialize(credentials);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            
            Console.WriteLine($"   📤 Enviando requisição de autenticação...");
            var response = await httpClient.PostAsync(_config.AuthPath, content);
            var respStr = await response.Content.ReadAsStringAsync();

            httpClient.Dispose();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"   ❌ FALHA na autenticação");
                Console.WriteLine($"   📊 Status Code: {(int)response.StatusCode} ({response.StatusCode})");
                Console.WriteLine($"   📋 Reason Phrase: {response.ReasonPhrase}");
                Console.WriteLine($"   📄 Response Body: {respStr}");
                Console.WriteLine($"   🔗 URL: {fullUrl}");
                throw new Exception($"Auth failed: {(int)response.StatusCode} {respStr}");
            }

            // Deserializar com opções case-insensitive para suportar "token" e "Token"
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var auth = JsonSerializer.Deserialize<CacheAuthResponse>(respStr, options);
            
            if (auth == null || string.IsNullOrEmpty(auth.Token))
            {
                Console.WriteLine($"   ❌ Token não encontrado na resposta");
                Console.WriteLine($"   📄 Response Body: {respStr}");
                throw new Exception("Token não encontrado na resposta de autenticação");
            }

            Console.WriteLine($"   ✅ Autenticação bem-sucedida");
            Console.WriteLine($"   🎫 Token obtido (tamanho: {auth.Token.Length} caracteres)");
            if (!string.IsNullOrWhiteSpace(auth.Expires))
            {
                Console.WriteLine($"   ⏰ Expira em: {auth.Expires}");
            }
            return auth.Token;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Console.WriteLine($"   ❌ TIMEOUT na autenticação");
            Console.WriteLine($"   ⏱️  Timeout após 30 segundos");
            Console.WriteLine($"   🔗 URL: {fullUrl}");
            httpClient?.Dispose();
            throw new Exception($"Timeout na autenticação: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"   ❌ ERRO HTTP na autenticação");
            Console.WriteLine($"   🔗 URL: {fullUrl}");
            Console.WriteLine($"   📋 Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"   📄 Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   📄 Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
            httpClient?.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ ERRO na autenticação");
            Console.WriteLine($"   🔗 URL: {fullUrl}");
            Console.WriteLine($"   📋 Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"   📄 Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   📄 Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
            httpClient?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        // Nada a fazer - HttpClient é criado localmente e já é descartado
    }
}

