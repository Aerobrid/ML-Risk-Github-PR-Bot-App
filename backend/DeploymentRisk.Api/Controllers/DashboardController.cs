using Microsoft.AspNetCore.Mvc;
using DeploymentRisk.Api.Services;

namespace DeploymentRisk.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly RiskLogStore _store;

    public DashboardController(RiskLogStore store)
    {
        _store = store;
    }

    [HttpGet("logs")]
    public IActionResult GetLogs()
    {
        return Ok(_store.GetAll());
    }
}
