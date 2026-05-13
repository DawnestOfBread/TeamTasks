using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamTasks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserOrganizations_Organizations_OrganizationsId",
                table: "UserOrganizations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserOrganizations_Users_UsersId",
                table: "UserOrganizations");

            migrationBuilder.RenameColumn(
                name: "UsersId",
                table: "UserOrganizations",
                newName: "OrganizationId");

            migrationBuilder.RenameColumn(
                name: "OrganizationsId",
                table: "UserOrganizations",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserOrganizations_UsersId",
                table: "UserOrganizations",
                newName: "IX_UserOrganizations_OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Provider_ExternalId",
                table: "Users",
                columns: new[] { "Provider", "ExternalId" },
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_UserOrganizations_Organizations_OrganizationId",
                table: "UserOrganizations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserOrganizations_Users_UserId",
                table: "UserOrganizations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserOrganizations_Organizations_OrganizationId",
                table: "UserOrganizations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserOrganizations_Users_UserId",
                table: "UserOrganizations");

            migrationBuilder.DropIndex(
                name: "IX_Users_Provider_ExternalId",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                table: "UserOrganizations",
                newName: "UsersId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "UserOrganizations",
                newName: "OrganizationsId");

            migrationBuilder.RenameIndex(
                name: "IX_UserOrganizations_OrganizationId",
                table: "UserOrganizations",
                newName: "IX_UserOrganizations_UsersId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserOrganizations_Organizations_OrganizationsId",
                table: "UserOrganizations",
                column: "OrganizationsId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserOrganizations_Users_UsersId",
                table: "UserOrganizations",
                column: "UsersId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
