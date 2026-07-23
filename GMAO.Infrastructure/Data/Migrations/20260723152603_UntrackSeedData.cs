using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UntrackSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally left blank.
            // We want EF Core to stop tracking the Seed data, 
            // but we DO NOT want to delete the actual records from the database.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Societes",
                columns: new[] { "Id", "Adresse", "CodeTenant", "CreatedAt", "EmailContact", "IsActive", "Nom" },
                values: new object[] { 1, null, "tenant-midi", new DateTime(2026, 1, 10, 12, 0, 0, 0, DateTimeKind.Utc), "admin@midi.com", true, "Conserverie du Midi S.A." });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Avatar", "CreatedAt", "Email", "IsActive", "Nom", "PasswordHash", "Prenom", "RoleId", "SocieteId", "Telephone" },
                values: new object[] { 1, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "superadmin@gmao.com", true, "Super", "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918", "Admin", 7, null, "+21699999999" });
        }
    }
}
