using DeploymentRisk.Api.Models.Entities;

namespace DeploymentRisk.Api.Repositories;

public class NoOpRiskRepository : IRiskRepository
{
    private readonly ILogger<NoOpRiskRepository> _logger;

    public NoOpRiskRepository(ILogger<NoOpRiskRepository> logger)
    {
        _logger = logger;
    }

    public Task<Guid> SaveAssessmentAsync(RiskAssessmentEntity assessment)
    {
        _logger.LogInformation("DB disabled - assessment not persisted: {Id} for {Repo}",
            assessment.Id, assessment.RepositoryFullName);
        return Task.FromResult(assessment.Id);
    }

    public Task<RiskAssessmentEntity?> GetAssessmentAsync(Guid id)
    {
        _logger.LogWarning("DB disabled - returning null for assessment {Id}", id);
        return Task.FromResult<RiskAssessmentEntity?>(null);
    }

    public Task<List<RiskAssessmentEntity>> GetAssessmentsByRepositoryAsync(string repoFullName, int pageSize = 50, int skip = 0)
    {
        _logger.LogWarning("DB disabled - returning empty list for repository {Repo}", repoFullName);
        return Task.FromResult(new List<RiskAssessmentEntity>());
    }

    public Task<List<RiskAssessmentEntity>> GetRecentAssessmentsAsync(int count = 100)
    {
        _logger.LogWarning("DB disabled - returning empty list");
        return Task.FromResult(new List<RiskAssessmentEntity>());
    }
}
