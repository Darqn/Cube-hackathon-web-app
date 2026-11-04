using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cube.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Name = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Department = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Summary = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Category = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Importance = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    PublishAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    AllTenants = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAcknowledgement = table.Column<bool>(type: "boolean", nullable: false),
                    TenantIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    PremisesIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Building = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Section = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Floor = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    LastError = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Premises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Type = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Address = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Building = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Section = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Floor = table.Column<int>(type: "integer", nullable: true),
                    Area = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Utilities = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Status = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Premises", x => x.Id);
                    table.CheckConstraint("positive_area", "\"Area\" > 0");
                    table.CheckConstraint("positive_rent", "\"MonthlyRent\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Inn = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Email = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Phone = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Blocked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    ClaimValue = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Listings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PremisesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Listings_Premises_PremisesId",
                        column: x => x.PremisesId,
                        principalTable: "Premises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    Blocked = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    RequestedOrganization = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    RequestedInn = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    SecurityStamp = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Name = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.UniqueConstraint("AK_Files_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Files_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Number = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.UniqueConstraint("AK_Invoices_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("invoice_amounts", "\"Amount\" > 0 AND \"Paid\" >= 0 AND \"Paid\" <= \"Amount\"");
                    table.ForeignKey(
                        name: "FK_Invoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PremisesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    SignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leases", x => x.Id);
                    table.UniqueConstraint("AK_Leases_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("lease_dates", "\"StartsOn\" <= \"EndsOn\"");
                    table.ForeignKey(
                        name: "FK_Leases_Premises_PremisesId",
                        column: x => x.PremisesId,
                        principalTable: "Premises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PremisesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Unit = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    LastValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meters", x => x.Id);
                    table.UniqueConstraint("AK_Meters_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("meter_baseline", "\"LastValue\" >= 0");
                    table.ForeignKey(
                        name: "FK_Meters_Premises_PremisesId",
                        column: x => x.PremisesId,
                        principalTable: "Premises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Meters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Read = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NewsId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_News_NewsId",
                        column: x => x.NewsId,
                        principalTable: "News",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receipts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    ClaimValue = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Name = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Value = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PremisesId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssigneeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subject = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Phone = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Email = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Status = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.UniqueConstraint("AK_Requests_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Requests_AspNetUsers_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requests_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requests_Premises_PremisesId",
                        column: x => x.PremisesId,
                        principalTable: "Premises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRecord_Invoices_TenantId_InvoiceId",
                        columns: x => new { x.TenantId, x.InvoiceId },
                        principalTable: "Invoices",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentRecord_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Number = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PremisesId = table.Column<Guid>(type: "uuid", nullable: true),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Files_TenantId_FileId",
                        columns: x => new { x.TenantId, x.FileId },
                        principalTable: "Files",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_Leases_TenantId_LeaseId",
                        columns: x => new { x.TenantId, x.LeaseId },
                        principalTable: "Leases",
                        principalColumns: new[] { "TenantId", "Id" });
                    table.ForeignKey(
                        name: "FK_Documents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ReviewReason = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.Id);
                    table.CheckConstraint("reading_monotonic", "\"Value\" >= \"PreviousValue\"");
                    table.ForeignKey(
                        name: "FK_Readings_Meters_TenantId_MeterId",
                        columns: x => new { x.TenantId, x.MeterId },
                        principalTable: "Meters",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Readings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Files_TenantId_FileId",
                        columns: x => new { x.TenantId, x.FileId },
                        principalTable: "Files",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attachments_Requests_TenantId_RequestId",
                        columns: x => new { x.TenantId, x.RequestId },
                        principalTable: "Requests",
                        principalColumns: new[] { "TenantId", "Id" });
                    table.ForeignKey(
                        name: "FK_Attachments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inquiries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inquiries_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inquiries_Requests_TenantId_RequestId",
                        columns: x => new { x.TenantId, x.RequestId },
                        principalTable: "Requests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inquiries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Internal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.UniqueConstraint("AK_Messages_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Messages_Requests_TenantId_RequestId",
                        columns: x => new { x.TenantId, x.RequestId },
                        principalTable: "Requests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestEvents_Requests_TenantId_RequestId",
                        columns: x => new { x.TenantId, x.RequestId },
                        principalTable: "Requests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId",
                table: "AspNetUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TenantId_CreatedAt",
                table: "Attachments",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TenantId_FileId",
                table: "Attachments",
                columns: new[] { "TenantId", "FileId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TenantId_RequestId",
                table: "Attachments",
                columns: new[] { "TenantId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Code",
                table: "Categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_CreatedAt",
                table: "Documents",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_FileId",
                table: "Documents",
                columns: new[] { "TenantId", "FileId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_LeaseId",
                table: "Documents",
                columns: new[] { "TenantId", "LeaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Files_TenantId_CreatedAt",
                table: "Files",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Inquiries_ListingId",
                table: "Inquiries",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Inquiries_TenantId_CreatedAt",
                table: "Inquiries",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Inquiries_TenantId_ListingId",
                table: "Inquiries",
                columns: new[] { "TenantId", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inquiries_TenantId_RequestId",
                table: "Inquiries",
                columns: new[] { "TenantId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ExternalId",
                table: "Invoices",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_CreatedAt",
                table: "Invoices",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_PremisesId",
                table: "Leases",
                column: "PremisesId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_TenantId_CreatedAt",
                table: "Leases",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_PremisesId",
                table: "Listings",
                column: "PremisesId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_CreatedAt",
                table: "Messages",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TenantId_RequestId",
                table: "Messages",
                columns: new[] { "TenantId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_Meters_PremisesId",
                table: "Meters",
                column: "PremisesId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_SerialNumber",
                table: "Meters",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meters_TenantId_CreatedAt",
                table: "Meters",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_CreatedAt",
                table: "Notifications",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Provider_Kind_ResourceId",
                table: "Outbox",
                columns: new[] { "Provider", "Kind", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Status_CreatedAt",
                table: "Outbox",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecord_Provider_TransactionId",
                table: "PaymentRecord",
                columns: new[] { "Provider", "TransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecord_TenantId_CreatedAt",
                table: "PaymentRecord",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecord_TenantId_InvoiceId",
                table: "PaymentRecord",
                columns: new[] { "TenantId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Readings_TenantId_CreatedAt",
                table: "Readings",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Readings_TenantId_MeterId",
                table: "Readings",
                columns: new[] { "TenantId", "MeterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_NewsId_UserId",
                table: "Receipts",
                columns: new[] { "NewsId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TenantId_CreatedAt",
                table: "Receipts",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestEvents_TenantId_CreatedAt",
                table: "RequestEvents",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestEvents_TenantId_RequestId",
                table: "RequestEvents",
                columns: new[] { "TenantId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_AssigneeId_Status",
                table: "Requests",
                columns: new[] { "AssigneeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CategoryId",
                table: "Requests",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_PremisesId",
                table: "Requests",
                column: "PremisesId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_TenantId_CreatedAt",
                table: "Requests",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TokenHash",
                table: "Sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                table: "Sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Inn",
                table: "Tenants",
                column: "Inn",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "Audit");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Inquiries");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "PaymentRecord");

            migrationBuilder.DropTable(
                name: "Readings");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "RequestEvents");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "Leases");

            migrationBuilder.DropTable(
                name: "Listings");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Meters");

            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Premises");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
