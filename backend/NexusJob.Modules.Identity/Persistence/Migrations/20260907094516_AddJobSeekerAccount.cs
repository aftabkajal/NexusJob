using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusJob.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobSeekerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_seeker_account",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_seeker_account", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_seeker_account_email",
                schema: "identity",
                table: "job_seeker_account",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_seeker_account",
                schema: "identity");
        }
    }
}
