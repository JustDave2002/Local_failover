using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Local_Failover.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxAndAppliedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Direction",
                table: "Outbox");

            migrationBuilder.RenameColumn(
                name: "SentAtUtc",
                table: "Outbox",
                newName: "SentUtc");

            migrationBuilder.RenameColumn(
                name: "Payload",
                table: "Outbox",
                newName: "PayloadJson");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Outbox",
                newName: "CreatedUtc");

            migrationBuilder.AlterColumn<string>(
                name: "Entity",
                table: "Outbox",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "AckedUtc",
                table: "Outbox",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "Outbox",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Outbox",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AppliedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppliedEvents", x => x.Id);
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

            migrationBuilder.DropIndex(
                name: "IX_Outbox_AckedUtc_CreatedUtc",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "AckedUtc",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Outbox");

            migrationBuilder.RenameColumn(
                name: "SentUtc",
                table: "Outbox",
                newName: "SentAtUtc");

            migrationBuilder.RenameColumn(
                name: "PayloadJson",
                table: "Outbox",
                newName: "Payload");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Outbox",
                newName: "CreatedAtUtc");

            migrationBuilder.AlterColumn<string>(
                name: "Entity",
                table: "Outbox",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "Outbox",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
