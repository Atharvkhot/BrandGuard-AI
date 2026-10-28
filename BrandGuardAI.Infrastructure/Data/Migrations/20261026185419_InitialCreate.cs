using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BrandGuardAI.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandGuidelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandGuidelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ContentText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuidelineChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandGuidelineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChunkIndex = table.Column<int>(type: "int", nullable: false),
                    ChunkText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    SectionTitle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    EmbeddingData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidelineChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidelineChunks_BrandGuidelines_BrandGuidelineId",
                        column: x => x.BrandGuidelineId,
                        principalTable: "BrandGuidelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RuleValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceGuidelineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationRules_BrandGuidelines_SourceGuidelineId",
                        column: x => x.SourceGuidelineId,
                        principalTable: "BrandGuidelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ValidationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OverallRiskLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalFindings = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationRuns_ContentSubmissions_ContentSubmissionId",
                        column: x => x.ContentSubmissionId,
                        principalTable: "ContentSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_ContentSubmissions_ContentSubmissionId",
                        column: x => x.ContentSubmissionId,
                        principalTable: "ContentSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_ValidationRuns_ValidationRunId",
                        column: x => x.ValidationRunId,
                        principalTable: "ValidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ValidationFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuidelineChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssueType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OriginalText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuggestedFix = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationFindings_GuidelineChunks_GuidelineChunkId",
                        column: x => x.GuidelineChunkId,
                        principalTable: "GuidelineChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ValidationFindings_ValidationRuns_ValidationRunId",
                        column: x => x.ValidationRunId,
                        principalTable: "ValidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ValidationRules",
                columns: new[] { "Id", "Description", "IsActive", "RuleName", "RuleType", "RuleValue", "Severity", "SourceGuidelineId" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Marketing content must not contain absolute outcome guarantees or promise 100% certainty.", true, "No Absolute Guarantees", "AbsoluteGuarantee", "\\b(guarantees?|guaranteed|100%\\s+results?|100%\\s+success|never\\s+fails?|zero\\s+risk|risk[\\s-]free)\\b", "High", null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "Unsubstantiated claims claiming #1 ranking or world-best status require documented proof and accreditation.", true, "Superlative Claims Verification", "RestrictedPhrase", "\\b(best\\s+in\\s+the\\s+world|#1\\s+in\\s+the\\s+world|unmatched|superior\\s+to\\s+all|unbeatable)\\b", "High", null },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "Marketing copy must never claim to cure illnesses or misrepresent medical clinical clearance.", true, "Prohibited Medical Claims", "ProhibitedTerm", "\\b(cures?|eliminates\\s+disease|miracle\\s+cure|fda[\\s-]certified|clinically\\s+proven\\s+cure)\\b", "High", null },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "Unapproved financial promises and return guarantees are strictly prohibited.", true, "Financial Guarantee Prohibited", "ProhibitedTerm", "\\b(get\\s+rich|double\\s+your\\s+money|guaranteed\\s+returns?|fail[\\s-]safe\\s+profit)\\b", "High", null },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "Promotional offers and outcome statements must include standard disclaimer language.", true, "Required Promotional Disclaimer", "RequiredDisclaimer", "terms\\s+and\\s+conditions\\s+apply|results\\s+may\\s+vary|individual\\s+results\\s+may\\s+differ", "Medium", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentSubmissions_CreatedAt",
                table: "ContentSubmissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GuidelineChunks_BrandGuidelineId",
                table: "GuidelineChunks",
                column: "BrandGuidelineId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidelineChunks_BrandGuidelineId_ChunkIndex",
                table: "GuidelineChunks",
                columns: new[] { "BrandGuidelineId", "ChunkIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ContentSubmissionId",
                table: "ReviewDecisions",
                column: "ContentSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ReviewedAt",
                table: "ReviewDecisions",
                column: "ReviewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ValidationRunId",
                table: "ReviewDecisions",
                column: "ValidationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationFindings_GuidelineChunkId",
                table: "ValidationFindings",
                column: "GuidelineChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationFindings_ValidationRunId",
                table: "ValidationFindings",
                column: "ValidationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationRules_RuleType_IsActive",
                table: "ValidationRules",
                columns: new[] { "RuleType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ValidationRules_SourceGuidelineId",
                table: "ValidationRules",
                column: "SourceGuidelineId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationRuns_ContentSubmissionId",
                table: "ValidationRuns",
                column: "ContentSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewDecisions");

            migrationBuilder.DropTable(
                name: "ValidationFindings");

            migrationBuilder.DropTable(
                name: "ValidationRules");

            migrationBuilder.DropTable(
                name: "GuidelineChunks");

            migrationBuilder.DropTable(
                name: "ValidationRuns");

            migrationBuilder.DropTable(
                name: "BrandGuidelines");

            migrationBuilder.DropTable(
                name: "ContentSubmissions");
        }
    }
}
