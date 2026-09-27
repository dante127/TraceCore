using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_ForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CasePersons_PersonId",
                table: "CasePersons",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseOrganizations_OrganizationId",
                table: "CaseOrganizations",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_CaseOrganizations_Organizations_OrganizationId",
                table: "CaseOrganizations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CasePersons_People_PersonId",
                table: "CasePersons",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseTasks_Cases_CaseId",
                table: "CaseTasks",
                column: "CaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Cases_CaseId",
                table: "Documents",
                column: "CaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Evidence_Cases_CaseId",
                table: "Evidence",
                column: "CaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Investigations_Cases_CaseId",
                table: "Investigations",
                column: "CaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RiskAssessmentHistories_Cases_CaseId",
                table: "RiskAssessmentHistories",
                column: "CaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseOrganizations_Organizations_OrganizationId",
                table: "CaseOrganizations");

            migrationBuilder.DropForeignKey(
                name: "FK_CasePersons_People_PersonId",
                table: "CasePersons");

            migrationBuilder.DropForeignKey(
                name: "FK_CaseTasks_Cases_CaseId",
                table: "CaseTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Cases_CaseId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Evidence_Cases_CaseId",
                table: "Evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_Investigations_Cases_CaseId",
                table: "Investigations");

            migrationBuilder.DropForeignKey(
                name: "FK_RiskAssessmentHistories_Cases_CaseId",
                table: "RiskAssessmentHistories");

            migrationBuilder.DropIndex(
                name: "IX_CasePersons_PersonId",
                table: "CasePersons");

            migrationBuilder.DropIndex(
                name: "IX_CaseOrganizations_OrganizationId",
                table: "CaseOrganizations");
        }
    }
}
