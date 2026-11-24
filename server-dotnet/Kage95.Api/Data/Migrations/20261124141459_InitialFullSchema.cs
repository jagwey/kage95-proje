using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Kage95.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialFullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mangas",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    japanese_title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    author = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    genres = table.Column<string>(type: "json", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    cover = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    catalog_rating = table.Column<decimal>(type: "decimal(3,1)", nullable: false),
                    chapters = table.Column<uint>(type: "int unsigned", nullable: false),
                    year = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    views = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    accent = table.Column<string>(type: "char(7)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mangas", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    first_name = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    last_name = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false),
                    username = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "user"),
                    bio = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    avatar_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chapters",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    manga_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    chapter_number = table.Column<decimal>(type: "decimal(6,1)", nullable: false),
                    title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapters", x => x.id);
                    table.ForeignKey(
                        name: "FK_chapters_mangas_manga_id",
                        column: x => x.manga_id,
                        principalTable: "mangas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    manga_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    user_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    body = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_comments_mangas_manga_id",
                        column: x => x.manga_id,
                        principalTable: "mangas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "favorites",
                columns: table => new
                {
                    user_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    manga_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_favorites", x => new { x.user_id, x.manga_id });
                    table.ForeignKey(
                        name: "FK_favorites_mangas_manga_id",
                        column: x => x.manga_id,
                        principalTable: "mangas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_favorites_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    user_low = table.Column<uint>(type: "int unsigned", nullable: false),
                    user_high = table.Column<uint>(type: "int unsigned", nullable: false),
                    requested_by = table.Column<uint>(type: "int unsigned", nullable: false),
                    status = table.Column<string>(type: "enum('pending','accepted','blocked')", nullable: false, defaultValue: "pending"),
                    blocked_by = table.Column<uint>(type: "int unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friendships", x => new { x.user_low, x.user_high });
                    table.ForeignKey(
                        name: "FK_friendships_users_user_high",
                        column: x => x.user_high,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_friendships_users_user_low",
                        column: x => x.user_low,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "point_records",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    action = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    points = table.Column<int>(type: "int", nullable: false),
                    reference = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true),
                    note = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_point_records_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ratings",
                columns: table => new
                {
                    manga_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    user_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    score = table.Column<byte>(type: "tinyint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ratings", x => new { x.manga_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_ratings_mangas_manga_id",
                        column: x => x.manga_id,
                        principalTable: "mangas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ratings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "reading_lists",
                columns: table => new
                {
                    user_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    manga_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "reading"),
                    current_chapter = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reading_lists", x => new { x.user_id, x.manga_id });
                    table.ForeignKey(
                        name: "FK_reading_lists_mangas_manga_id",
                        column: x => x.manga_id,
                        principalTable: "mangas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reading_lists_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    token_hash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    user_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.token_hash);
                    table.ForeignKey(
                        name: "FK_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chapter_pages",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    chapter_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    page_number = table.Column<uint>(type: "int unsigned", nullable: false),
                    image_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter_pages", x => x.id);
                    table.ForeignKey(
                        name: "FK_chapter_pages_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalTable: "chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_low = table.Column<uint>(type: "int unsigned", nullable: false),
                    user_high = table.Column<uint>(type: "int unsigned", nullable: false),
                    sender_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    body = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    manga_id = table.Column<uint>(type: "int unsigned", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_friendships_user_low_user_high",
                        columns: x => new { x.user_low, x.user_high },
                        principalTable: "friendships",
                        principalColumns: new[] { "user_low", "user_high" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_mangas_manga_id",
                        column: x => x.manga_id,
                        principalTable: "mangas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_pages_chapter_id_page_number",
                table: "chapter_pages",
                columns: new[] { "chapter_id", "page_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chapters_manga_id_chapter_number",
                table: "chapters",
                columns: new[] { "manga_id", "chapter_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comments_manga_id_id",
                table: "comments",
                columns: new[] { "manga_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_user_id",
                table: "comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_favorites_manga_id",
                table: "favorites",
                column: "manga_id");

            migrationBuilder.CreateIndex(
                name: "IX_friendships_user_high_user_low",
                table: "friendships",
                columns: new[] { "user_high", "user_low" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_manga_id",
                table: "messages",
                column: "manga_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_user_low_user_high_id",
                table: "messages",
                columns: new[] { "user_low", "user_high", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_point_records_created_at",
                table: "point_records",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_point_records_user_id_action_reference",
                table: "point_records",
                columns: new[] { "user_id", "action", "reference" });

            migrationBuilder.CreateIndex(
                name: "IX_ratings_user_id",
                table: "ratings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reading_lists_manga_id",
                table: "reading_lists",
                column: "manga_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_expires_at",
                table: "sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_user_id",
                table: "sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chapter_pages");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "favorites");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "point_records");

            migrationBuilder.DropTable(
                name: "ratings");

            migrationBuilder.DropTable(
                name: "reading_lists");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "chapters");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "mangas");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
