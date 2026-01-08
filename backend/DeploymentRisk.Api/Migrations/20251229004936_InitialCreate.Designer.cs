using System;
using DeploymentRisk.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DeploymentRisk.Api.Migrations
{
    // tell EF Core which DB contecxt this migration applies to
    [DbContext(typeof(RiskDbContext))]
    // unique identifer for migration
    [Migration("20251229004936_InitialCreate")]
    // generated designer for this migration (ef makes this)
    partial class InitialCreate
    {
        /// <inheritdoc />
        // builds the model "shape" that this migration targets
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            // use identity columns by default for sql server
            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            // app-level configuration key/value storage
            modelBuilder.Entity("DeploymentRisk.Api.Models.Entities.ConfigurationEntity", b =>
                {
                    b.Property<string>("Key")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Key");

                    b.ToTable("Configurations");
                });

            // main risk assessment records captured from events/prs
            modelBuilder.Entity("DeploymentRisk.Api.Models.Entities.RiskAssessmentEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Author")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Branch")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<double?>("BugScore")
                        .HasColumnType("float");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("EventType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GitHubCommentUrl")
                        .HasColumnType("nvarchar(max)");

                    b.Property<double?>("MLScore")
                        .HasColumnType("float");

                    b.Property<string>("MetricsJson")
                        .HasColumnType("nvarchar(max)");

                    b.Property<double>("OverallRiskScore")
                        .HasColumnType("float");

                    b.Property<int?>("PullRequestNumber")
                        .HasColumnType("int");

                    b.Property<string>("RepositoryFullName")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("RiskFactorsJson")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RiskLevel")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<double?>("RuleBasedScore")
                        .HasColumnType("float");

                    b.Property<double?>("SecurityScore")
                        .HasColumnType("float");

                    b.Property<string>("Sha")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    // helpful indexes for common queries
                    b.HasIndex("CreatedAt");

                    b.HasIndex("RepositoryFullName", "CreatedAt");

                    b.ToTable("RiskAssessments");
                });

            // raw webhook intake + processing flagging
            modelBuilder.Entity("DeploymentRisk.Api.Models.Entities.WebhookEventEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("EventType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PayloadJson")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("Processed")
                        .HasColumnType("bit");

                    b.Property<DateTime>("ReceivedAt")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    // quick time-based lookup
                    b.HasIndex("ReceivedAt");

                    b.ToTable("WebhookEvents");
                });
#pragma warning restore 612, 618
        }
    }
}
