using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;

namespace SmallBusinessCRM.Api.Services;

public interface ICrmCacheService
{
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task InvalidateReportsCacheAsync(string tenantId = "default", CancellationToken ct = default);
}

public class CrmCacheService(
    IDistributedCache cache,
    ILogger<CrmCacheService> logger) : ICrmCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var cachedBytes = await cache.GetAsync(key, ct);
            if (cachedBytes is not null && cachedBytes.Length > 0)
            {
                var cachedObj = JsonSerializer.Deserialize<T>(cachedBytes, JsonOptions);
                if (cachedObj is not null)
                {
                    logger.LogDebug("Cache HIT for key {Key}", key);
                    return cachedObj;
                }
            }
        }
        catch (Exception ex)
        {
            // Graceful fallback: If Redis is unavailable or times out, proceed to DB
            logger.LogWarning(ex, "Distributed cache unavailable for key '{Key}'. Falling back directly to data source.", key);
        }

        // Cache MISS or Cache failure: execute factory
        var result = await factory();
        if (result is null) return default;

        try
        {
            var serialized = JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions);
            var entryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromMinutes(5)
            };
            await cache.SetAsync(key, serialized, entryOptions, ct);
            logger.LogDebug("Cache SET for key {Key}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write key '{Key}' to distributed cache. Continuing without caching.", key);
        }

        return result;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
            logger.LogDebug("Cache REMOVE for key {Key}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to evict key '{Key}' from distributed cache.", key);
        }
    }

    public async Task InvalidateReportsCacheAsync(string tenantId = "default", CancellationToken ct = default)
    {
        await RemoveAsync($"crm:{tenantId}:reports:dashboard", ct);
        await RemoveAsync($"crm:{tenantId}:reports:analytics", ct);
    }
}
