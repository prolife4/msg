using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vmx.Messenger.Migrations.MessengerContext
{
    /// <inheritdoc />
    public partial class Messenger_9fe089296eb94b9ca8d4341d3fc33cce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "comment",
                schema: "messenger",
                table: "telegram_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "comment",
                schema: "messenger",
                table: "task_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "comment",
                schema: "messenger",
                table: "email_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "comment",
                schema: "messenger",
                table: "telegram_messages");

            migrationBuilder.DropColumn(
                name: "comment",
                schema: "messenger",
                table: "task_messages");

            migrationBuilder.DropColumn(
                name: "comment",
                schema: "messenger",
                table: "email_messages");
        }
    }
}
