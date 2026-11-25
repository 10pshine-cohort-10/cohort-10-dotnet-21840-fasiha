using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaskManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedAccountRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "13b17f93-f0d0-4b7e-90a1-71a3dabd6bd0", "member-initial", "Member", "MEMBER" },
                    { "ffbcfe34-ea3a-49bb-b54a-a974561064b9", "admin-initial", "Admin", "ADMIN" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "13b17f93-f0d0-4b7e-90a1-71a3dabd6bd0");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "ffbcfe34-ea3a-49bb-b54a-a974561064b9");
        }
    }
}
