using DeploymentRisk.Api.Data;
using DeploymentRisk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeploymentRisk.Api.Repositories;

// SQL Server–backed config storage
public class SqlServerConfigRepository : IConfigRepository
{
    private readonly RiskDbContext _db;

    public SqlServerConfigRepository(RiskDbContext db)
    {
        _db = db;
    }

    // fetch single config value fom DB by primary key
    public async Task<string?> GetValueAsync(string key)
    {
        var config = await _db.Configurations.FindAsync(key);
        return config?.Value;
    }

    // create or update a config value
    public async Task SetValueAsync(string key, string value, string category)
    {
        // check if key exists
        var config = await _db.Configurations.FindAsync(key);

        // if it does not exist -> create one
        // else update current one
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

    // fetch all config values in a category
    public async Task<Dictionary<string, string>> GetCategoryAsync(string category)
    {
        return await _db.Configurations
            .Where(c => c.Category == category)
            .ToDictionaryAsync(c => c.Key, c => c.Value);
    }
}
