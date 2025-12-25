using Microsoft.AspNetCore.Mvc;
using DeploymentRisk.Api.Models;
using DeploymentRisk.Api.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace DeploymentRisk.Api.Controllers;

[ApiController]
[Route("api/risk")]
public class RiskController : ControllerBase 
{
    private readonly MlClient _ml;
    private readonly RiskLogStore _store;

    public RiskController(MlClient ml, RiskLogStore store)
    {
        _ml = ml;
        _store = store;
    }


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
