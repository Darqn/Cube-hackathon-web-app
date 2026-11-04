using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cube.Api.Migrations
{
    /// <inheritdoc />
    public partial class NotificationEventIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_Kind_ResourceId",
                table: "Notifications");

            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "Notifications",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_DeduplicationKey",
                table: "Notifications",
                columns: new[] { "UserId", "DeduplicationKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_DeduplicationKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Kind_ResourceId",
                table: "Notifications",
                columns: new[] { "UserId", "Kind", "ResourceId" },
                unique: true);
        }
    }
}
