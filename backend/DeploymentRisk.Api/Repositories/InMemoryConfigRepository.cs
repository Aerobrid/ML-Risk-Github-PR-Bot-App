using System.Collections.Concurrent;

namespace DeploymentRisk.Api.Repositories;

public class InMemoryConfigRepository : IConfigRepository
{
    private readonly ConcurrentDictionary<string, (string Value, string Category)> _store = new();
    private readonly ILogger<InMemoryConfigRepository> _logger;

    public InMemoryConfigRepository(ILogger<InMemoryConfigRepository> logger)
    {
        _logger = logger;
    }

    public Task<string?> GetValueAsync(string key)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            return Task.FromResult<string?>(entry.Value);
        }
        return Task.FromResult<string?>(null);
    }

    public Task SetValueAsync(string key, string value, string category)
    {
        _store[key] = (value, category);
        _logger.LogInformation("DB disabled - config stored in memory: {Key} = {Value}", key, value);
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, string>> GetCategoryAsync(string category)
    {
        var result = _store
            .Where(kvp => kvp.Value.Category == category)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
        return Task.FromResult(result);
    }
}
