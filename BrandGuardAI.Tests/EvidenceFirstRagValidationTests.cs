using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Ai;
using BrandGuardAI.Infrastructure.Chunking;
using BrandGuardAI.Infrastructure.Data;
using BrandGuardAI.Infrastructure.Pdf;
using BrandGuardAI.Infrastructure.Retrieval;
using BrandGuardAI.Infrastructure.Rules;
using BrandGuardAI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrandGuardAI.Tests;

public class EvidenceFirstRagValidationTests
{
    private (ApplicationDbContext Context, ValidationOrchestrator Orchestrator, ContentSubmissionService ContentSvc, Guid ActiveGuidelineId, Guid ActiveChunkId) SetupTestEnvironment()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        // Seed an active brand guideline and chunk
        var guideline = new BrandGuideline
        {
            Id = Guid.NewGuid(),
            Title = "Acme Official Marketing & Claims Policy",
            FileName = "Acme_Policy.pdf",
            FilePath = "uploads/guidelines/Acme_Policy.pdf",
            Status = GuidelineStatus.Ready,
            IsActive = true,
            UploadedAt = DateTime.UtcNow
        };
        context.BrandGuidelines.Add(guideline);

        var chunkVector = EmbeddingService.GenerateDeterministicVector("Our marketing policy strictly prohibits absolute guarantees and unverified superlatives.");
        var chunk = new GuidelineChunk
        {
            Id = Guid.NewGuid(),
            BrandGuidelineId = guideline.Id,
            ChunkIndex = 0,
            PageNumber = 3,
            SectionTitle = "Section 2.1: Prohibited Outcome Guarantees",
            ChunkText = "Section 2.1: Prohibited Outcome Guarantees. All public communications must never state or imply 100% success or zero risk.",
            EmbeddingData = System.Text.Json.JsonSerializer.Serialize(chunkVector),
            CreatedAt = DateTime.UtcNow
        };
        context.GuidelineChunks.Add(chunk);
        context.SaveChanges();

        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();

        var embeddingSvc = new EmbeddingService(httpClient, config, NullLogger<EmbeddingService>.Instance);
        var searchSvc = new SemanticSearchService(context, embeddingSvc, NullLogger<SemanticSearchService>.Instance);
        var ruleEngine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var llmSvc = new LlmValidationService(httpClient, config, NullLogger<LlmValidationService>.Instance);

        var orchestrator = new ValidationOrchestrator(context, ruleEngine, searchSvc, llmSvc, NullLogger<ValidationOrchestrator>.Instance);
        var contentSvc = new ContentSubmissionService(context, NullLogger<ContentSubmissionService>.Instance);

        return (context, orchestrator, contentSvc, guideline.Id, chunk.Id);
    }

    [Fact]
    public async Task Validation_With_Deterministic_Rule_Should_Have_DeterministicRule_Provenance()
    {
        // Arrange
        var (context, orchestrator, contentSvc, _, _) = SetupTestEnvironment();
        var submission = await contentSvc.CreateSubmissionAsync(new CreateContentSubmissionDto(
            "Product Guarantee Headline",
            "Social Media Post",
            "This revolutionary tool guarantees 100% results for any business."
        ));

        // Act
        var result = await orchestrator.RunValidationAsync(submission.Id);

        // Assert
        result.Should().NotBeNull();
        result.Findings.Should().NotBeEmpty();

        var guaranteeFinding = result.Findings.FirstOrDefault(f =>
            f.IssueType.Contains("Guarantee", StringComparison.OrdinalIgnoreCase) ||
            f.OriginalText.Contains("guarantees", StringComparison.OrdinalIgnoreCase));

        guaranteeFinding.Should().NotBeNull();
        guaranteeFinding!.EvidenceProvenance.Should().Be(EvidenceProvenanceTypes.DeterministicRule);
        guaranteeFinding.HasVerifiedEvidence.Should().BeTrue();
        guaranteeFinding.SourceGuidelineTitle.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Validation_With_Direct_Guideline_Citation_Should_Have_DirectGuidelineMatch_Provenance()
    {
        // Arrange
        var (context, _, contentSvc, _, activeChunkId) = SetupTestEnvironment();

        var submission = await contentSvc.CreateSubmissionAsync(new CreateContentSubmissionDto(
            "Quarterly Newsletter",
            "Email Newsletter",
            "General corporate update on new features and quarterly results."
        ));

        // Simulate a completed validation run with a verified guideline chunk match
        var run = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = submission.Id,
            Status = "Completed",
            OverallRiskLevel = RiskSeverity.Medium,
            TotalFindings = 1,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        context.ValidationRuns.Add(run);

        var finding = new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = run.Id,
            GuidelineChunkId = activeChunkId,
            IssueType = "Brand Tone Compliance",
            Severity = RiskSeverity.Medium,
            OriginalText = "revolutionary solution",
            Explanation = "Overly promotional tone conflicts with technical brand voice.",
            SuggestedFix = "Use objective descriptive language.",
            EvidenceText = "Section 2.1: Prohibited Outcome Guarantees. All public communications must never state or imply 100% success or zero risk.",
            CreatedAt = DateTime.UtcNow
        };
        context.ValidationFindings.Add(finding);
        await context.SaveChangesAsync();

        // Act
        var result = await contentSvc.GetLatestValidationResultAsync(submission.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Findings.Should().HaveCount(1);
        var dto = result.Findings[0];

        dto.EvidenceProvenance.Should().Be(EvidenceProvenanceTypes.DirectGuidelineMatch);
        dto.HasVerifiedEvidence.Should().BeTrue();
        dto.GuidelineChunkId.Should().Be(activeChunkId);
        dto.SourceGuidelineTitle.Should().Be("Acme Official Marketing & Claims Policy");
        dto.PageNumber.Should().Be(3);
        dto.SectionTitle.Should().Be("Section 2.1: Prohibited Outcome Guarantees");
        dto.EvidenceText.Should().Contain("Section 2.1");
    }

    [Fact]
    public async Task Validation_With_Fabricated_Or_NonRetrieved_ChunkId_Should_Reject_Citation_And_Mark_UnverifiedAIClaim()
    {
        // Arrange
        var (context, _, contentSvc, _, _) = SetupTestEnvironment();

        var submission = await contentSvc.CreateSubmissionAsync(new CreateContentSubmissionDto(
            "Uncorroborated Marketing Copy",
            "Blog Article",
            "Our revolutionary tool speeds up team workflows by 500%."
        ));

        var fakeChunkId = Guid.NewGuid(); // Chunk does NOT exist in DB

        var run = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = submission.Id,
            Status = "Completed",
            OverallRiskLevel = RiskSeverity.Medium,
            TotalFindings = 1,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        context.ValidationRuns.Add(run);

        var finding = new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = run.Id,
            GuidelineChunkId = fakeChunkId, // Fabricated citation
            IssueType = "Unsupported Claim",
            Severity = RiskSeverity.Medium,
            OriginalText = "speeds up team workflows by 500%",
            Explanation = "Statistical efficiency claim requires official case study documentation.",
            SuggestedFix = "Cite audited customer benchmarks.",
            EvidenceText = "Fabricated guideline text that is not in any database record.",
            CreatedAt = DateTime.UtcNow
        };
        context.ValidationFindings.Add(finding);
        await context.SaveChangesAsync();

        // Act
        var result = await contentSvc.GetLatestValidationResultAsync(submission.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Findings.Should().HaveCount(1);
        var dto = result.Findings[0];

        // Fabricated chunk must be rejected: GuidelineChunkId nullified, provenance UnverifiedAIClaim
        dto.GuidelineChunkId.Should().BeNull();
        dto.SourceGuidelineTitle.Should().BeNull();
        dto.PageNumber.Should().BeNull();
        dto.EvidenceProvenance.Should().Be(EvidenceProvenanceTypes.UnverifiedAIClaim);
        dto.HasVerifiedEvidence.Should().BeFalse();
        dto.EvidenceText.Should().BeEmpty();
    }

    [Fact]
    public async Task Validation_AI_Finding_Without_Evidence_Should_Have_UnverifiedAIClaim_And_Empty_Evidence()
    {
        // Arrange
        var (context, _, contentSvc, _, _) = SetupTestEnvironment();

        var submission = await contentSvc.CreateSubmissionAsync(new CreateContentSubmissionDto(
            "Casual Social Copy",
            "Social Media Post",
            "Hey folks check out our sweet new release!"
        ));

        var run = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = submission.Id,
            Status = "Completed",
            OverallRiskLevel = RiskSeverity.Low,
            TotalFindings = 1,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        context.ValidationRuns.Add(run);

        var finding = new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = run.Id,
            GuidelineChunkId = null,
            IssueType = "Informal Voice",
            Severity = RiskSeverity.Low,
            OriginalText = "Hey folks",
            Explanation = "Informal greeting does not align with corporate executive brand tone.",
            SuggestedFix = "Use professional opening.",
            EvidenceText = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
        context.ValidationFindings.Add(finding);
        await context.SaveChangesAsync();

        // Act
        var result = await contentSvc.GetLatestValidationResultAsync(submission.Id);

        // Assert
        result.Should().NotBeNull();
        var dto = result!.Findings[0];

        dto.GuidelineChunkId.Should().BeNull();
        dto.EvidenceProvenance.Should().Be(EvidenceProvenanceTypes.UnverifiedAIClaim);
        dto.HasVerifiedEvidence.Should().BeFalse();
        dto.SourceGuidelineTitle.Should().BeNull();
        dto.PageNumber.Should().BeNull();
        dto.EvidenceText.Should().BeEmpty();
    }

    [Fact]
    public void LlmValidationService_ParseAndVerify_Should_Revoke_Fabricated_ChunkId()
    {
        // Arrange
        var retrievedChunks = new List<ChunkSearchResult>
        {
            new ChunkSearchResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Real Guideline",
                1,
                "Section 1",
                "Actual approved brand text.",
                0.85f
            )
        };

        var fakeChunkId = Guid.NewGuid();
        var jsonResponse = $@"{{
            ""findings"": [
                {{
                    ""issueCategory"": ""Unsupported Claim"",
                    ""severity"": ""Medium"",
                    ""affectedText"": ""untested claim"",
                    ""explanation"": ""No benchmark data provided."",
                    ""suggestedCorrection"": ""Remove claim."",
                    ""citedChunkId"": ""{fakeChunkId}"",
                    ""evidenceQuote"": ""Fabricated quote""
                }}
            ],
            ""overallAssessment"": ""Requires revision.""
        }}";

        var httpClient = new HttpClient();
        var config = new ConfigurationBuilder().Build();
        var service = new LlmValidationService(httpClient, config, NullLogger<LlmValidationService>.Instance);

        // Act - Invoke via private method reflection or testing parser behavior
        var method = typeof(LlmValidationService).GetMethod(
            "ParseAndVerifyLlmResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var parsed = method?.Invoke(service, new object[] { jsonResponse, retrievedChunks }) as LlmValidationResponse;

        // Assert
        parsed.Should().NotBeNull();
        parsed!.Findings.Should().HaveCount(1);
        var finding = parsed.Findings[0];

        // Fabricated citedChunkId must be rejected and revoked to null
        finding.CitedChunkId.Should().BeNull();
    }
}
