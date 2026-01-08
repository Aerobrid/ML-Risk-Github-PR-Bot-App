// EF Core Migration File (DB schema version control)
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeploymentRisk.Api.Migrations
{
    /// <inheritdoc />
    // NOTE: partial allows EF to generate extra code in background
    // base migration class gives Up and Down methods
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        // run this method to create tables, columns, indexes, etc. on a `dotnet ef databse update`
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // creating configuration table
            migrationBuilder.CreateTable(
                // its name
                name: "Configurations",
                // according to ConfigurationEntity properties
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Key);
                });

            // risk assessment table, simlar setups
            // `nvarchar(max)` is a string with unlimited length
            migrationBuilder.CreateTable(
                name: "RiskAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryFullName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PullRequestNumber = table.Column<int>(type: "int", nullable: true),
                    Sha = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverallRiskScore = table.Column<double>(type: "float", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuleBasedScore = table.Column<double>(type: "float", nullable: true),
                    MLScore = table.Column<double>(type: "float", nullable: true),
                    SecurityScore = table.Column<double>(type: "float", nullable: true),
                    BugScore = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GitHubCommentUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiskFactorsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessments", x => x.Id);
                });

            // webhook event table
            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Processed = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEvents", x => x.Id);
                });

            // adding indexes make DB queries faster for common operations like by repo or CreatedAt/RecievedAt date
            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_CreatedAt",
                table: "RiskAssessments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_RepositoryFullName_CreatedAt",
                table: "RiskAssessments",
                columns: new[] { "RepositoryFullName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_ReceivedAt",
                table: "WebhookEvents",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        // this method undoes migration for rollbacks (removes tables instead of adding them)
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.DropTable(
                name: "RiskAssessments");

            migrationBuilder.DropTable(
                name: "WebhookEvents");
        }
    }
}
