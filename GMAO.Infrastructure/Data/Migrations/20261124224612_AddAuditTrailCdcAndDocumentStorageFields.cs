using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTrailCdcAndDocumentStorageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncidentId",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256Checksum",
                table: "Documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageProvider",
                table: "Documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateFin",
                table: "Campagnes",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.CreateTable(
                name: "AuditTrailEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UserRole = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SocieteId = table.Column<int>(type: "integer", nullable: true),
                    SocieteName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BusinessActionLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OldValuesJson = table.Column<string>(type: "text", nullable: true),
                    NewValuesJson = table.Column<string>(type: "text", nullable: true),
                    ChangedProperties = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TransactionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsCompensated = table.Column<bool>(type: "boolean", nullable: false),
                    CompensatedEntryId = table.Column<long>(type: "bigint", nullable: true),
                    CompensatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompensatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrailEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CdcEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SchemaName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TableName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SocieteId = table.Column<int>(type: "integer", nullable: true),
                    TransactionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Lsn = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CommitTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OldValuesJson = table.Column<string>(type: "text", nullable: true),
                    NewValuesJson = table.Column<string>(type: "text", nullable: true),
                    SlotName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PublicationName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CdcEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CdcWorkerStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SlotName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PublicationName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PostgresVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WalLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MaxReplicationSlots = table.Column<int>(type: "integer", nullable: true),
                    MaxWalSenders = table.Column<int>(type: "integer", nullable: true),
                    SlotActive = table.Column<bool>(type: "boolean", nullable: true),
                    LastConsumedLsn = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CurrentServerWalLsn = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReplicationLagBytes = table.Column<long>(type: "bigint", nullable: true),
                    RetainedWalBytes = table.Column<long>(type: "bigint", nullable: true),
                    WalStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TotalEventsProcessed = table.Column<long>(type: "bigint", nullable: false),
                    LastEventAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CdcWorkerStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IncidentId",
                table: "Documents",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SocieteId",
                table: "Documents",
                column: "SocieteId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_StorageProvider",
                table: "Documents",
                column: "StorageProvider");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailEntries_EntityName",
                table: "AuditTrailEntries",
                column: "EntityName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailEntries_SocieteId",
                table: "AuditTrailEntries",
                column: "SocieteId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailEntries_TimestampUtc",
                table: "AuditTrailEntries",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailEntries_TransactionId",
                table: "AuditTrailEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailEntries_UserId",
                table: "AuditTrailEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CdcEvents_CommitTimestampUtc",
                table: "CdcEvents",
                column: "CommitTimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CdcEvents_Lsn_TableName_Operation_RecordId",
                table: "CdcEvents",
                columns: new[] { "Lsn", "TableName", "Operation", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CdcEvents_SocieteId",
                table: "CdcEvents",
                column: "SocieteId");

            migrationBuilder.CreateIndex(
                name: "IX_CdcEvents_TableName",
                table: "CdcEvents",
                column: "TableName");

            migrationBuilder.CreateIndex(
                name: "IX_CdcEvents_TransactionId",
                table: "CdcEvents",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CdcWorkerStates_SlotName",
                table: "CdcWorkerStates",
                column: "SlotName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DemandesIntervention_IncidentId",
                table: "Documents",
                column: "IncidentId",
                principalTable: "DemandesIntervention",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Societes_SocieteId",
                table: "Documents",
                column: "SocieteId",
                principalTable: "Societes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DemandesIntervention_IncidentId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Societes_SocieteId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "AuditTrailEntries");

            migrationBuilder.DropTable(
                name: "CdcEvents");

            migrationBuilder.DropTable(
                name: "CdcWorkerStates");

            migrationBuilder.DropIndex(
                name: "IX_Documents_IncidentId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_SocieteId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_StorageProvider",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BucketName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IncidentId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Sha256Checksum",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "StorageProvider",
                table: "Documents");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateFin",
                table: "Campagnes",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
