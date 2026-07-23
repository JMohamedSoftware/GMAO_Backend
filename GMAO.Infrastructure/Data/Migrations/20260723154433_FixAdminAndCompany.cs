using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixAdminAndCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Update Societe Id=1 to cicam (safely)
            migrationBuilder.Sql(@"
                UPDATE ""Societes""
                SET ""Nom"" = 'cicam', ""EmailContact"" = 'cicam@midi.com'
                WHERE ""Id"" = 1;
            ");

            // 2. Insert SuperAdmin safely without tracking
            migrationBuilder.Sql(@"
                INSERT INTO ""Users"" (""Id"", ""Nom"", ""Prenom"", ""Email"", ""PasswordHash"", ""Telephone"", ""RoleId"", ""SocieteId"", ""IsActive"", ""CreatedAt"", ""Avatar"")
                VALUES (1, 'Super', 'Admin', 'superadmin@gmao.com', '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', '+21699999999', 7, NULL, true, '2026-01-01T00:00:00Z', NULL)
                ON CONFLICT (""Id"") DO UPDATE 
                SET ""Email"" = 'superadmin@gmao.com', ""PasswordHash"" = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', ""IsActive"" = true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
