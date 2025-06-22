using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capstone2.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuPackages",
                columns: table => new
                {
                    MenuPackageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuPackageName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoOfMainDish = table.Column<int>(type: "int", nullable: false),
                    NoOfSideDish = table.Column<int>(type: "int", nullable: true),
                    NoOfDessert = table.Column<int>(type: "int", nullable: true),
                    NoOfRice = table.Column<int>(type: "int", nullable: true),
                    NoOfSoftDrinks = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuPackages", x => x.MenuPackageId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuPackages");
        }
    }
}
