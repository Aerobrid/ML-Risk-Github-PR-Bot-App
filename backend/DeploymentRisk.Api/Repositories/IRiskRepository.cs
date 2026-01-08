using DeploymentRisk.Api.Models.Entities;

namespace DeploymentRisk.Api.Repositories;

// interface used for Repository folder code
// details the methods needed to be used by a class implementing it
public interface IRiskRepository
{
    Task<Guid> SaveAssessmentAsync(RiskAssessmentEntity assessment);
    Task<RiskAssessmentEntity?> GetAssessmentAsync(Guid id);
    Task<List<RiskAssessmentEntity>> GetAssessmentsByRepositoryAsync(string repoFullName, int pageSize = 50, int skip = 0);
    Task<List<RiskAssessmentEntity>> GetRecentAssessmentsAsync(int count = 100);
}
