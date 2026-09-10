using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusJob.Modules.Applications.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "applications");

            migrationBuilder.CreateTable(
                name: "application",
                schema: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_seeker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_job_posting_id_job_seeker_id",
                schema: "applications",
                table: "application",
                columns: new[] { "job_posting_id", "job_seeker_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application",
                schema: "applications");
        }
    }
}
