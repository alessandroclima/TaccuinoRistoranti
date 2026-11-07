using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaccuinoRistoranti.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMealProposedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProposedBy",
                table: "Meals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposedBy",
                table: "Meals");
        }
    }
}
