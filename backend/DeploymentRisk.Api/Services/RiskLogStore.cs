using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services;

public class RiskLogStore
{
    // TEMP in-memory store 
    private static readonly List<RiskResponse> _logs = new();

    public void Add(RiskResponse response)
    {
        _logs.Add(response);
    }

    public IReadOnlyList<RiskResponse> GetAll()
    {
        return _logs;
    }
}
