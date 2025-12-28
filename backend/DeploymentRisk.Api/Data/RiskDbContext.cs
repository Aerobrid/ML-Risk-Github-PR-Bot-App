using DeploymentRisk.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeploymentRisk.Api.Data;

public class RiskDbContext : DbContext
{
    public RiskDbContext(DbContextOptions<RiskDbContext> options) : base(options) { }

    public DbSet<RiskAssessmentEntity> RiskAssessments => Set<RiskAssessmentEntity>();
    public DbSet<ConfigurationEntity> Configurations => Set<ConfigurationEntity>();
    public DbSet<WebhookEventEntity> WebhookEvents => Set<WebhookEventEntity>();

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
