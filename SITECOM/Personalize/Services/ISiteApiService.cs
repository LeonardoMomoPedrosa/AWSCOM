using Personalize.Models;

namespace Personalize.Services;

public interface ISiteApiService
{
    Task<bool> InvalidateAsync(CacheInvalidateRequest request);
    Task<bool> InvalidateAsync(IEnumerable<CacheInvalidateRequest> requests);
}

