using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusJob.Modules.JobPostings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialJobPostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "job_postings");

            migrationBuilder.CreateTable(
                name: "job_posting",
                schema: "job_postings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_posting", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_posting_owner_company_id",
                schema: "job_postings",
                table: "job_posting",
                column: "owner_company_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_posting",
                schema: "job_postings");
        }
    }
}
