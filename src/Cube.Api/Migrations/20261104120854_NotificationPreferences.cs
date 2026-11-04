using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cube.Api.Migrations
{
    /// <inheritdoc />
    public partial class NotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InvoiceReminders",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MeterReminders",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NewsNotifications",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Kind_ResourceId",
                table: "Notifications",
                columns: new[] { "UserId", "Kind", "ResourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_Kind_ResourceId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "InvoiceReminders",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MeterReminders",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NewsNotifications",
                table: "AspNetUsers");
        }
    }
}
