using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using SmallBusinessCRM.Api.Services;
using Xunit;

namespace SmallBusinessCRM.Tests;

public class CacheAndResilienceTests
{
    private class FaultyDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Simulated Redis connection failure");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Simulated Redis connection failure");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("Simulated Redis connection failure");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new InvalidOperationException("Simulated Redis connection failure");

        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => throw new InvalidOperationException("Simulated Redis connection failure");
        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Simulated Redis connection failure");
    }

    [Fact]
    public async Task CrmCacheService_FallsBackToFactory_WhenDistributedCacheFails()
    {
        var faultyCache = new FaultyDistributedCache();
        var cacheService = new CrmCacheService(faultyCache, NullLogger<CrmCacheService>.Instance);

        var factoryExecuted = false;
        var result = await cacheService.GetOrSetAsync("test:key", () =>
        {
            factoryExecuted = true;
            return Task.FromResult("live-database-data");
        });

        Assert.True(factoryExecuted, "Factory should execute as fallback when Redis cache throws.");
        Assert.Equal("live-database-data", result);
    }

    [Fact]
    public async Task ResiliencePolicyService_RetriesOnTransientException()
    {
        var resilience = new ResiliencePolicyService(NullLogger<ResiliencePolicyService>.Instance);

        var attempts = 0;
        var result = await resilience.ExecuteAsync<string>(async ct =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new DbUpdateConcurrencyException("Simulated transient concurrency conflict");
            }
            return await Task.FromResult("success-after-retry");
        });

        Assert.Equal(2, attempts);
        Assert.Equal("success-after-retry", result);
    }
}
