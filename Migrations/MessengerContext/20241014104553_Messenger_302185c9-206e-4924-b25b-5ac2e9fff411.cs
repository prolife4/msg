using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vmx.Messenger.Migrations.MessengerContext
{
    /// <inheritdoc />
    public partial class Messenger_302185c9206e4924b25b5ac2e9fff411 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "relay_url",
                schema: "messenger",
                table: "kafka_clients",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "relay_url",
                schema: "messenger",
                table: "kafka_clients");
        }
    }
}
