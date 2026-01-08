using DeploymentRisk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeploymentRisk.Api.Data;

// main database context
public class RiskDbContext : DbContext
{
    public RiskDbContext(DbContextOptions<RiskDbContext> options) : base(options) { }

    // DbSets (Tables in the database)
    public DbSet<RiskAssessmentEntity> RiskAssessments => Set<RiskAssessmentEntity>();
    public DbSet<ConfigurationEntity> Configurations => Set<ConfigurationEntity>();
    public DbSet<WebhookEventEntity> WebhookEvents => Set<WebhookEventEntity>();

    // method EF Core calls once when building the model
    // configure primary keys, indexes, and other DB-level rules
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RiskAssessmentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RepositoryFullName, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<ConfigurationEntity>(entity =>
        {
            entity.HasKey(e => e.Key);
        });

        modelBuilder.Entity<WebhookEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReceivedAt);
        });
    }
}
