using Microsoft.AspNetCore.Mvc;
using DeploymentRisk.Api.Models;
using DeploymentRisk.Api.Services;

namespace DeploymentRisk.Api.Controllers;

[ApiController]
[Route("api/risk")]
// analyzes pull requests and returns risk predictions
public class RiskController : ControllerBase 
{
    private readonly MlClient _ml;
    private readonly RiskLogStore _store;

    // inject ml client (calls ML Model to predict risk score) and risk log store (in-memory or persistent)
    public RiskController(MlClient ml, RiskLogStore store)
    {
        _ml = ml;
        _store = store;
    }


    // predict risk score for a pr and store the result
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] RiskRequest request)
    {
        var result = await _ml.PredictAsync(request);

        if (result != null)
        {
            _store.Add(result);
        }

        return Ok(result);
    }

}
