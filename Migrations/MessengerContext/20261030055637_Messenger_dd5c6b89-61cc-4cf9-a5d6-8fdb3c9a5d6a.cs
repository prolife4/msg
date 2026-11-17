using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vmx.Messenger.Migrations.MessengerContext
{
    /// <inheritdoc />
    public partial class Messenger_dd5c6b8961cc4cf9a5d68fdb3c9a5d6a : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "max_clients",
                schema: "messenger",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chat_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    chat_id = table.Column<long>(type: "bigint", nullable: true),
                    token = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_max_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "max_messages",
                schema: "messenger",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    client_id = table.Column<int>(type: "integer", nullable: true),
                    caption = table.Column<string>(type: "character varying(350)", maxLength: 350, nullable: true),
                    body = table.Column<string>(type: "text", nullable: true),
                    attachments = table.Column<string>(type: "text", nullable: true),
                    attachment_count = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    date_sent = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    text_format = table.Column<string>(type: "text", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_max_messages", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "max_clients",
                schema: "messenger");

            migrationBuilder.DropTable(
                name: "max_messages",
                schema: "messenger");
        }
    }
}
