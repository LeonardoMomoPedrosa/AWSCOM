using System.Text.Json.Serialization;

namespace Personalize.Models;

public class CacheAuthResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
    
    [JsonPropertyName("tokenType")]
    public string? TokenType { get; set; }
    
    [JsonPropertyName("expires")]
    public string? Expires { get; set; }
    
    [JsonPropertyName("expiresIn")]
    public int? ExpiresIn { get; set; }
}

