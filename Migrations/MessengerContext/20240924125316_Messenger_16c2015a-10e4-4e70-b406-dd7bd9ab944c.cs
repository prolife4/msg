using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vmx.Messenger.Migrations.MessengerContext
{
    /// <inheritdoc />
    public partial class Messenger_16c2015a10e44e70b406dd7bd9ab944c : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "auto_offset_reset",
                schema: "messenger",
                table: "kafka_clients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ssl_ca_location",
                schema: "messenger",
                table: "kafka_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ssl_certificate_location",
                schema: "messenger",
                table: "kafka_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ssl_key_location",
                schema: "messenger",
                table: "kafka_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "use_ssl",
                schema: "messenger",
                table: "kafka_clients",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auto_offset_reset",
                schema: "messenger",
                table: "kafka_clients");

            migrationBuilder.DropColumn(
                name: "ssl_ca_location",
                schema: "messenger",
                table: "kafka_clients");

            migrationBuilder.DropColumn(
                name: "ssl_certificate_location",
                schema: "messenger",
                table: "kafka_clients");

            migrationBuilder.DropColumn(
                name: "ssl_key_location",
                schema: "messenger",
                table: "kafka_clients");

            migrationBuilder.DropColumn(
                name: "use_ssl",
                schema: "messenger",
                table: "kafka_clients");
        }
    }
}
