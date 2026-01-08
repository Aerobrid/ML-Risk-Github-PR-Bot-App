using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeploymentRisk.Api.Migrations
{
    /// <inheritdoc />
    // name is descriptive enough for what this migration does
    public partial class UseDateTimeOffsetForCreatedAt : Migration
    {
        /// <inheritdoc />
        // applies migration which updates `CreatedAt` column in `RiskAssessments`
        // from `DateTime` to `DateTimeOffset` so it can handle time zones more accurately
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "RiskAssessments",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }

        /// <inheritdoc />
        // reverts column from `DateTimeOffset` back to `DateTime`
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "RiskAssessments",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");
        }
    }
}
