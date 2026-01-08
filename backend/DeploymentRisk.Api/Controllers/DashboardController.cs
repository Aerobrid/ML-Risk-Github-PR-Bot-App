using Microsoft.AspNetCore.Mvc;
using DeploymentRisk.Api.Repositories;

namespace DeploymentRisk.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
// serves risk assessment summaries and trends for the dashboard
public class DashboardController : ControllerBase
{
    private readonly IRiskRepository _repository;
    private readonly ILogger<DashboardController> _logger;

    // inject risk repo and logger
    public DashboardController(IRiskRepository repository, ILogger<DashboardController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // returns most recent `count` risk assessments across all permission-granted repo's
    [HttpGet("assessments")]
    public async Task<IActionResult> GetRecentAssessments([FromQuery] int count = 100)
    {
        try
        {
            var assessments = await _repository.GetRecentAssessmentsAsync(count);
            return Ok(assessments);
        }
        // throw back a 500 if exception catched + log it
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recent assessments");
            return StatusCode(500, new { message = "Error fetching assessments" });
        }
    }

    // returns risk assessments of a specific repository
    [HttpGet("assessments/{repoFullName}")]
    // page size is used for pagination (how many results per page, adjust if necessary)
    public async Task<IActionResult> GetRepositoryAssessments(string repoFullName, [FromQuery] int pageSize = 50)
    {
        try
        {
            var assessments = await _repository.GetAssessmentsByRepositoryAsync(repoFullName, pageSize);
            return Ok(assessments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching assessments for {Repo}", repoFullName);
            return StatusCode(500, new { message = "Error fetching assessments" });
        }
    }

    // sums up statistics from the most recent assessments (adjust as necessary)
    [HttpGet("stats")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var assessments = await _repository.GetRecentAssessmentsAsync(1000);

            var stats = new
            {
                Total = assessments.Count,
                LowRisk = assessments.Count(a => a.RiskLevel == "LOW"),
                MediumRisk = assessments.Count(a => a.RiskLevel == "MEDIUM"),
                HighRisk = assessments.Count(a => a.RiskLevel == "HIGH"),
                CriticalRisk = assessments.Count(a => a.RiskLevel == "CRITICAL"),
                AverageScore = assessments.Any() ? assessments.Average(a => a.OverallRiskScore) : 0
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching statistics");
            return StatusCode(500, new { message = "Error fetching statistics" });
        }
    }

    // legacy endpoint for backwards compatibility
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs()
    {
        return await GetRecentAssessments(100);
    }
}
