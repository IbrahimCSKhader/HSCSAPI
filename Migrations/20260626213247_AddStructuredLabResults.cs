using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLabResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabTestTemplates",
                columns: table => new
                {
                    LabTestTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SpecimenType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreparationInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestTemplates", x => x.LabTestTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "LabTestFieldDefinitions",
                columns: table => new
                {
                    LabTestFieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabTestTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LoincCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: true),
                    ReferenceRange = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AllowedValuesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestFieldDefinitions", x => x.LabTestFieldDefinitionId);
                    table.ForeignKey(
                        name: "FK_LabTestFieldDefinitions_LabTestTemplates_LabTestTemplateId",
                        column: x => x.LabTestTemplateId,
                        principalTable: "LabTestTemplates",
                        principalColumn: "LabTestTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabTestResults",
                columns: table => new
                {
                    LabTestResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabTestRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabTestTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LaboratoryTechnologistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    AccessionNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SpecimenCondition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SpecimenNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PdfFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PdfChecksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PdfFileSizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    PdfGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestResults", x => x.LabTestResultId);
                    table.ForeignKey(
                        name: "FK_LabTestResults_LabTestRequests_LabTestRequestId",
                        column: x => x.LabTestRequestId,
                        principalTable: "LabTestRequests",
                        principalColumn: "LabTestRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabTestResults_LabTestTemplates_LabTestTemplateId",
                        column: x => x.LabTestTemplateId,
                        principalTable: "LabTestTemplates",
                        principalColumn: "LabTestTemplateId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabTestResults_LaboratoryTechnologists_LaboratoryTechnologistId",
                        column: x => x.LaboratoryTechnologistId,
                        principalTable: "LaboratoryTechnologists",
                        principalColumn: "LaboratoryTechnologistId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabTestResultValues",
                columns: table => new
                {
                    LabTestResultValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabTestResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabTestFieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FieldLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumericValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceRange = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Flag = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestResultValues", x => x.LabTestResultValueId);
                    table.ForeignKey(
                        name: "FK_LabTestResultValues_LabTestFieldDefinitions_LabTestFieldDefinitionId",
                        column: x => x.LabTestFieldDefinitionId,
                        principalTable: "LabTestFieldDefinitions",
                        principalColumn: "LabTestFieldDefinitionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabTestResultValues_LabTestResults_LabTestResultId",
                        column: x => x.LabTestResultId,
                        principalTable: "LabTestResults",
                        principalColumn: "LabTestResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabTestFieldDefinitions_LabTestTemplateId_Code",
                table: "LabTestFieldDefinitions",
                columns: new[] { "LabTestTemplateId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestFieldDefinitions_LabTestTemplateId_DisplayOrder",
                table: "LabTestFieldDefinitions",
                columns: new[] { "LabTestTemplateId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestResults_AccessionNumber",
                table: "LabTestResults",
                column: "AccessionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestResults_CompletedAt",
                table: "LabTestResults",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestResults_LaboratoryTechnologistId",
                table: "LabTestResults",
                column: "LaboratoryTechnologistId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestResults_LabTestRequestId",
                table: "LabTestResults",
                column: "LabTestRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestResults_LabTestTemplateId",
                table: "LabTestResults",
                column: "LabTestTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestResultValues_LabTestFieldDefinitionId",
                table: "LabTestResultValues",
                column: "LabTestFieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTestResultValues_LabTestResultId_LabTestFieldDefinitionId",
                table: "LabTestResultValues",
                columns: new[] { "LabTestResultId", "LabTestFieldDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestTemplates_Code",
                table: "LabTestTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestTemplates_LoincCode",
                table: "LabTestTemplates",
                column: "LoincCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabTestResultValues");

            migrationBuilder.DropTable(
                name: "LabTestFieldDefinitions");

            migrationBuilder.DropTable(
                name: "LabTestResults");

            migrationBuilder.DropTable(
                name: "LabTestTemplates");
        }
    }
}
