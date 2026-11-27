using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameMemberRoleToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "13b17f93-f0d0-4b7e-90a1-71a3dabd6bd0",
                columns: new[] { "Name", "NormalizedName" },
                values: new object[] { "User", "USER" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "13b17f93-f0d0-4b7e-90a1-71a3dabd6bd0",
                columns: new[] { "Name", "NormalizedName" },
                values: new object[] { "Member", "MEMBER" });
        }
    }
}
