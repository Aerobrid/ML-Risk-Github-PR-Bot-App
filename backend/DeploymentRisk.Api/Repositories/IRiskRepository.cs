using DeploymentRisk.Api.Models.Entities;

namespace DeploymentRisk.Api.Repositories;

public interface IRiskRepository
{
    Task<Guid> SaveAssessmentAsync(RiskAssessmentEntity assessment);
    Task<RiskAssessmentEntity?> GetAssessmentAsync(Guid id);
    Task<List<RiskAssessmentEntity>> GetAssessmentsByRepositoryAsync(string repoFullName, int pageSize = 50, int skip = 0);
    Task<List<RiskAssessmentEntity>> GetRecentAssessmentsAsync(int count = 100);
}
