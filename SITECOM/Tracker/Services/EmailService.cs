using System.Net;

namespace Tracker.Services;

public class EmailService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public EmailService(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
    }

    public async Task<bool> ScheduleEmailAsync(int orderId, string rastreamentoJson)
    {
        try
        {
            var jsonEncoded = WebUtility.UrlEncode(rastreamentoJson);
            var url = $"{_baseUrl}?trx_tp=7&oid={orderId}&json={jsonEncoded}";
            
            var response = await _httpClient.GetAsync(url);
            
            // Considera sucesso se retornar 200 OK
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

