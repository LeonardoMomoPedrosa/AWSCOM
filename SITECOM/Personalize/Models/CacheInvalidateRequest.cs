namespace Personalize.Models;

public class CacheInvalidateRequest
{
    public string Region { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool CleanRegionInd { get; set; }
}

