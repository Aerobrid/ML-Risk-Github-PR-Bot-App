namespace DeploymentRisk.Api.Models.Entities;

public class ConfigurationEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
