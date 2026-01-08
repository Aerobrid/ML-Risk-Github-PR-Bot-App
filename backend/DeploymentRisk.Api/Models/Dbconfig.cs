namespace DeploymentRisk.Api.Models;

// helper class to store DB connection string (used in other repository files)
public class DbConfig
{
    public string ConnectionString { get; set; } = string.Empty;
}
