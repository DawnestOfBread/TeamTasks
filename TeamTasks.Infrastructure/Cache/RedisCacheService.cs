using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TeamTasks.Application.Cache;

namespace TeamTasks.Infrastructure.Cache;

public class RedisCacheService(IDistributedCache cache, JsonSerializerOptions jsonOptions) : ICacheService
{
	public async Task<T?> GetAsync<T>(string key)
	{
		string? cachedData = await cache.GetStringAsync(key);
		return string.IsNullOrEmpty(cachedData) ? default : JsonSerializer.Deserialize<T>(cachedData, jsonOptions);
	}

	public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
	{
		var jsonString = JsonSerializer.Serialize(value, jsonOptions);
        
		var options = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10)
		};

		await cache.SetStringAsync(key, jsonString, options);
	}

	public async Task RemoveAsync(string key)
	{
		await cache.RemoveAsync(key);
	}
}