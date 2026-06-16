using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace NovaTech.TerraTech.Platform.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    author_profile_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    target_profile_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    content = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    rating = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_comments", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "community_profiles",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    profile_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    nickname = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    reputation_score = table.Column<int>(type: "int", nullable: false),
                    public_bio = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    visibility_status = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_community_profiles", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    field_id = table.Column<int>(type: "int", nullable: false),
                    mac_address = table.Column<string>(type: "varchar(17)", maxLength: 17, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    last_sync = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_devices", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fields",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    profile_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    size_m2 = table.Column<double>(type: "double", nullable: false),
                    soil_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    latitude = table.Column<double>(type: "double", nullable: false),
                    longitude = table.Column<double>(type: "double", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_fields", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inventories",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    product_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    stock_quantity = table.Column<int>(type: "int", nullable: false),
                    warehouse_location = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_inventories", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    profile_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    is_read = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_alert = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_notifications", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    profile_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    product_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    payment_method = table.Column<int>(type: "int", nullable: false),
                    is_subscription = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_orders", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    image_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_products", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "profiles",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    fundo_name = table.Column<string>(type: "longtext", nullable: false),
                    contact_phone = table.Column<string>(type: "longtext", nullable: false),
                    moisture_threshold = table.Column<double>(type: "double", nullable: false),
                    temp_threshold = table.Column<double>(type: "double", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_profiles", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    device_id = table.Column<int>(type: "int", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    mean_value = table.Column<double>(type: "double", nullable: false),
                    variance = table.Column<double>(type: "double", nullable: false),
                    standard_deviation = table.Column<double>(type: "double", nullable: false),
                    technical_interpretation = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_reports", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    email_address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "longtext", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_users", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_users_email_address",
                table: "users",
                column: "email_address",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "community_profiles");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "fields");

            migrationBuilder.DropTable(
                name: "inventories");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "profiles");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
