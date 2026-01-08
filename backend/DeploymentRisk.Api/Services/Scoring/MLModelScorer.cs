using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services.Scoring;

// scorer class
public class MLModelScorer : IRiskScorer
{
    // to call ML Service over http
    private readonly MlClient _mlClient;
    // to read app settings / environment variables
    private readonly IConfiguration _config;
    // to write logs relating to class
    private readonly ILogger<MLModelScorer> _logger;

    public MLModelScorer(MlClient mlClient, IConfiguration config, ILogger<MLModelScorer> logger)
    {
        _mlClient = mlClient;
        _config = config;
        _logger = logger;
    }

    public string Name => "MLModel";

    public bool IsEnabled => _config.GetValue<bool>("RiskScoring:Enabled:MLModel");

    public async Task<ScorerResult> ScoreAsync(RiskContext context)
    {
        try
        {
            // Build ML Request
            var request = new RiskRequest
            {
                CommitCount = context.CommitCount,
                LinesChanged = context.LinesAdded + context.LinesDeleted,
                TestPassRate = 1.0, // Placeholder, as we don't have test results yet
                HourOfDay = context.Timestamp.Hour,
                DayOfWeek = (int)context.Timestamp.DayOfWeek,
                Files = context.Files
            };

            // Call ML Service
            var prediction = await _mlClient.PredictAsync(request);

            if (prediction == null)
            {
                // if ML Service responds but with no data
                return new ScorerResult
                {
                    Score = 0.5,
                    Level = "MEDIUM",
                    RiskFactors = new List<string> { "ML Service returned no prediction" }
                };
            }

            // if ML Service responds with data
            return new ScorerResult
            {
                Score = prediction.RiskScore,
                Level = prediction.RiskLevel,
                RiskFactors = prediction.Details.Select(d => $"{d.Key}: {d.Value}").ToList(),
                ScanReport = prediction.ScanReport
            };
        }
        catch (Exception ex)
        {
            // any failure gets a fallback 0/low risk score
            _logger.LogError(ex, "Failed to get ML prediction");
            return new ScorerResult
            {
                Score = 0.0,
                Level = "LOW",
                RiskFactors = new List<string> { $"ML Service Error: {ex.Message}" }
            };
        }
    }
}
