using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace NovaTech.TerraTech.Platform.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookRecommendationsAndBattery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "battery_level_percent",
                table: "devices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "webhook_recommendations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    field_id = table.Column<int>(type: "int", nullable: false),
                    device_id = table.Column<int>(type: "int", nullable: true),
                    fertility_zone_id = table.Column<int>(type: "int", nullable: true),
                    zone = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    action = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    duration_minutes = table.Column<int>(type: "int", nullable: false),
                    priority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    category = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    source = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    external_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_webhook_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "f_k_webhook_recommendations_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_webhook_recommendations_fields_field_id",
                        column: x => x.field_id,
                        principalTable: "fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_webhook_recommendations_created_at",
                table: "webhook_recommendations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "i_x_webhook_recommendations_device_id",
                table: "webhook_recommendations",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "i_x_webhook_recommendations_field_id",
                table: "webhook_recommendations",
                column: "field_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_recommendations");

            migrationBuilder.DropColumn(
                name: "battery_level_percent",
                table: "devices");
        }
    }
}
