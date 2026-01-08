namespace DeploymentRisk.Api.Repositories;

// similar setup to IRiskRepository.cs
// tells other classes implementing it how config needs to be stored
public interface IConfigRepository
{
    Task<string?> GetValueAsync(string key);
    Task SetValueAsync(string key, string value, string category);
    Task<Dictionary<string, string>> GetCategoryAsync(string category);
}
