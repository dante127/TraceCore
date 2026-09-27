using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TraceCore.Application.Common.Interfaces;

namespace TraceCore.Infrastructure.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(key, cancellationToken);
            if (bytes is null || bytes.Length == 0)
                return default;

            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for key '{Key}'. Falling back to live query.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(15)
            };

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(key, bytes, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for key '{Key}'. Continuing without cache.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache removal failed for key '{Key}'.", key);
        }
    }
}
