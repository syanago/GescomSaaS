using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GescomSaas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialDocumentLineSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "CommercialDocumentLines",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "CommercialDocumentLines");
        }
    }
}
