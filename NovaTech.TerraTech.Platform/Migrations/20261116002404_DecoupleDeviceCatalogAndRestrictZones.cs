using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaTech.TerraTech.Platform.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleDeviceCatalogAndRestrictZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_devices__sensor_catalog_item_sensor_code",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "f_k_fertility_zones__field_field_id",
                table: "fertility_zones");

            migrationBuilder.DropForeignKey(
                name: "f_k_fertility_zones_devices_device_id",
                table: "fertility_zones");

            migrationBuilder.AddForeignKey(
                name: "f_k_fertility_zones__field_field_id",
                table: "fertility_zones",
                column: "field_id",
                principalTable: "fields",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_fertility_zones_devices_device_id",
                table: "fertility_zones",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_fertility_zones__field_field_id",
                table: "fertility_zones");

            migrationBuilder.DropForeignKey(
                name: "f_k_fertility_zones_devices_device_id",
                table: "fertility_zones");

            migrationBuilder.AddForeignKey(
                name: "f_k_devices__sensor_catalog_item_sensor_code",
                table: "devices",
                column: "sensor_code",
                principalTable: "sensor_catalog_items",
                principalColumn: "sensor_code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_fertility_zones__field_field_id",
                table: "fertility_zones",
                column: "field_id",
                principalTable: "fields",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "f_k_fertility_zones_devices_device_id",
                table: "fertility_zones",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
