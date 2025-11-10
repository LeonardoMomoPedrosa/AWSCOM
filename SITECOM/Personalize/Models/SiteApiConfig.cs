namespace Personalize.Models;

public class SiteApiConfig
{
    public List<SiteApiServer> Servers { get; set; } = new();
    public string AuthPath { get; set; } = string.Empty;
    public string InvalidateApi { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TokenCacheMinutes { get; set; } = 50;
}

public class SiteApiServer
{
    public string BaseUrl { get; set; } = string.Empty;
}

