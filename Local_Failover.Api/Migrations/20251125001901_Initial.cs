using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Local_Failover.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppliedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppliedEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Customer = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leases",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastBeatAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leases", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "OpApplied",
                columns: table => new
                {
                    OpId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpApplied", x => x.OpId);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Entity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AckedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Customer = table.Column<string>(type: "TEXT", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Product = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_AckedUtc_CreatedUtc",
                table: "Outbox",
                columns: new[] { "AckedUtc", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppliedEvents");

            migrationBuilder.DropTable(
                name: "CustomerNotes");

            migrationBuilder.DropTable(
                name: "Leases");

            migrationBuilder.DropTable(
                name: "OpApplied");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "SalesOrders");

            migrationBuilder.DropTable(
                name: "StockMovements");
        }
    }
}
