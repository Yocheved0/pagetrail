using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PageTrail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DedupPagesIgnoringWww : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pages_JobId_UrlHash",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "UrlHash",
                table: "Pages");

            migrationBuilder.AddColumn<string>(
                name: "UrlKey",
                table: "Pages",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE [Pages] SET [UrlKey] = [Url]");

            migrationBuilder.AddColumn<byte[]>(
                name: "UrlKeyHash",
                table: "Pages",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: false,
                computedColumnSql: "CAST(HASHBYTES('SHA2_256', [UrlKey]) AS binary(32))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pages_JobId_UrlKeyHash",
                table: "Pages",
                columns: new[] { "JobId", "UrlKeyHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pages_JobId_UrlKeyHash",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "UrlKeyHash",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "UrlKey",
                table: "Pages");

            migrationBuilder.AddColumn<byte[]>(
                name: "UrlHash",
                table: "Pages",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: false,
                computedColumnSql: "CAST(HASHBYTES('SHA2_256', [Url]) AS binary(32))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pages_JobId_UrlHash",
                table: "Pages",
                columns: new[] { "JobId", "UrlHash" },
                unique: true);
        }
    }
}
