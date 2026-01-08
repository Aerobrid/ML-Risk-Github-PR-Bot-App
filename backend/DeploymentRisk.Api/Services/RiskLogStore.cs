using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services;

// service storing `RiskResponse` objects temporarily
// this is needed since the users have the option to NOT configure a DB for logging all risks
public class RiskLogStore
{
    // TEMP in-memory store, kind of like cache
    // private we don't want other classes accessing it!
    // TODO: could use ConcurrentQueue<RiskResponse> for thread-safety in future
    // ? GPT-5 suggests for large app uses without DB, I could configure a instance singleton via DI, research this future me!
    private static readonly List<RiskResponse> _logs = new();

    // to add new risk log
    public void Add(RiskResponse response)
    {
        _logs.Add(response);
    }

    // to return all risk logs
    public IReadOnlyList<RiskResponse> GetAll()
    {
        return _logs;
    }
}
