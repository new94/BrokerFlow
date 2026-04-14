using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace BrokerFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileMask = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileFormat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CsvSeparator = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CsvCustomSeparator = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Sources", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_Sources_Name", table: "Sources", column: "Name");

            migrationBuilder.CreateTable(
                name: "XmlTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_XmlTemplates", x => x.Id));

            migrationBuilder.CreateTable(
                name: "MappingConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    XmlTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SplitOutput = table.Column<bool>(type: "bit", nullable: false),
                    SplitConditionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SplitFileNamePattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_MappingConfigs", x => x.Id));

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MappingId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CronExpression = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Schedules", x => x.Id));

            migrationBuilder.CreateTable(
                name: "ProcessingJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MappingId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResultPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordsProcessed = table.Column<int>(type: "int", nullable: false),
                    FilesGenerated = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ProcessingJobs", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_ProcessingJobs_Status", table: "ProcessingJobs", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_ProcessingJobs_CreatedAt", table: "ProcessingJobs", column: "CreatedAt");

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_AuditEntries", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_AuditEntries_CreatedAt", table: "AuditEntries", column: "CreatedAt");

            migrationBuilder.CreateTable(
                name: "AppConfigs",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_AppConfigs", x => x.Key));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppConfigs");
            migrationBuilder.DropTable(name: "AuditEntries");
            migrationBuilder.DropTable(name: "ProcessingJobs");
            migrationBuilder.DropTable(name: "Schedules");
            migrationBuilder.DropTable(name: "MappingConfigs");
            migrationBuilder.DropTable(name: "XmlTemplates");
            migrationBuilder.DropTable(name: "Sources");
        }
    }
}
