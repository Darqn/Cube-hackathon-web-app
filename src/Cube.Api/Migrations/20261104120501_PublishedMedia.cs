using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cube.Api.Migrations
{
    /// <inheritdoc />
    public partial class PublishedMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublishedAsset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewsId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Name = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    State = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishedAsset", x => x.Id);
                    table.CheckConstraint("one_asset_parent", "(\"ListingId\" IS NULL) <> (\"NewsId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PublishedAsset_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublishedAsset_News_NewsId",
                        column: x => x.NewsId,
                        principalTable: "News",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublishedAsset_ListingId",
                table: "PublishedAsset",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_PublishedAsset_NewsId",
                table: "PublishedAsset",
                column: "NewsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublishedAsset");
        }
    }
}
