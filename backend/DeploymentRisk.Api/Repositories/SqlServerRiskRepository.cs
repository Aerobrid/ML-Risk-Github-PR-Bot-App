using DeploymentRisk.Api.Data;
using DeploymentRisk.Api.Models.Entities;
// we use Entity Framework Core (easier to work with DB using .NET objects)
using Microsoft.EntityFrameworkCore;

namespace DeploymentRisk.Api.Repositories;

// class talks to SQL Server + stores and retrieves `RiskAssessmentIdentity`
public class SqlServerRiskRepository : IRiskRepository
{
    private readonly RiskDbContext _db;
    private readonly ILogger<SqlServerRiskRepository> _logger;

    public SqlServerRiskRepository(RiskDbContext db, ILogger<SqlServerRiskRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    // save risk assessment to DB and return generated ID
    public async Task<Guid> SaveAssessmentAsync(RiskAssessmentEntity assessment)
    {
        _db.RiskAssessments.Add(assessment);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Saved risk assessment {Id} for {Repo}", assessment.Id, assessment.RepositoryFullName);
        return assessment.Id;
    }

    // fetch a record with ID given (primary key)
    public async Task<RiskAssessmentEntity?> GetAssessmentAsync(Guid id)
    {
        return await _db.RiskAssessments.FindAsync(id);
    }

    // fetch paginated results for a single repo
    public async Task<List<RiskAssessmentEntity>> GetAssessmentsByRepositoryAsync(string repoFullName, int pageSize = 50, int skip = 0)
    {
        return await _db.RiskAssessments
            .Where(a => a.RepositoryFullName == repoFullName)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
    }

    // fetches most recent assessments across all permission-granted repos
    public async Task<List<RiskAssessmentEntity>> GetRecentAssessmentsAsync(int count = 100)
    {
        return await _db.RiskAssessments
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}
