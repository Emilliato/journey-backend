using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnBridge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnerUserAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Learners",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Learners");
        }
    }
}
