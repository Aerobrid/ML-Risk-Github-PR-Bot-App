namespace DeploymentRisk.Api.Repositories;

public interface IConfigRepository
{
    Task<string?> GetValueAsync(string key);
    Task SetValueAsync(string key, string value, string category);
    Task<Dictionary<string, string>> GetCategoryAsync(string category);
}
