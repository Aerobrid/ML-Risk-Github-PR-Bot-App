using DeploymentRisk.Api.Data;
using DeploymentRisk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeploymentRisk.Api.Repositories;

public class SqlServerConfigRepository : IConfigRepository
{
    private readonly RiskDbContext _db;

    public SqlServerConfigRepository(RiskDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetValueAsync(string key)
    {
        var config = await _db.Configurations.FindAsync(key);
        return config?.Value;
    }

    public async Task SetValueAsync(string key, string value, string category)
    {
        var config = await _db.Configurations.FindAsync(key);
        if (config == null)
        {
            config = new ConfigurationEntity
            {
                Key = key,
                Value = value,
                Category = category,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Configurations.Add(config);
        }
        else
        {
            config.Value = value;
            config.Category = category;
            config.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<Dictionary<string, string>> GetCategoryAsync(string category)
    {
        return await _db.Configurations
            .Where(c => c.Category == category)
            .ToDictionaryAsync(c => c.Key, c => c.Value);
    }
}
