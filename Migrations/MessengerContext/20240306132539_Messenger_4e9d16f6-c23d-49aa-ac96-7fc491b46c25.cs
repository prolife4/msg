using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vmx.Messenger.Migrations.MessengerContext
{
    /// <inheritdoc />
    public partial class Messenger_4e9d16f6c23d49aaac967fc491b46c25 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attachments",
                schema: "messenger",
                table: "telegram_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attachments",
                schema: "messenger",
                table: "telegram_messages");
        }
    }
}
