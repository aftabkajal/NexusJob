using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusJob.Modules.Applications.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobSeekerSubmittedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_application_job_seeker_id_submitted_at",
                schema: "applications",
                table: "application",
                columns: new[] { "job_seeker_id", "submitted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_application_job_seeker_id_submitted_at",
                schema: "applications",
                table: "application");
        }
    }
}
