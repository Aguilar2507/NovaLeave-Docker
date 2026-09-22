using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaLeave.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddVacationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VacationRequests_Owner_Status_Range",
                table: "VacationRequests",
                columns: new[] { "OwnerId", "Status", "StartDate", "EndDate" },
                filter: "[Status] IN ('Pending', 'Approved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VacationRequests_Owner_Status_Range",
                table: "VacationRequests");
        }
    }
}
