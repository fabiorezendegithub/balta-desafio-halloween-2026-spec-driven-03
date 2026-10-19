using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Encurtador.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Links",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CodeKey = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TargetUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ClickCount = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    ManagementKeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Links", x => x.Id);
                    table.CheckConstraint("CK_Link_ClickCount_NonNegative", "\"ClickCount\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "UQ_Link_CodeKey",
                table: "Links",
                column: "CodeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Links");
        }
    }
}
