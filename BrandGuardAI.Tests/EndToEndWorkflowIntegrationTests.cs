using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Ai;
using BrandGuardAI.Infrastructure.Chunking;
using BrandGuardAI.Infrastructure.Data;
using BrandGuardAI.Infrastructure.Pdf;
using BrandGuardAI.Infrastructure.Retrieval;
using BrandGuardAI.Infrastructure.Rules;
using BrandGuardAI.Infrastructure.Services;
using BrandGuardAI.Infrastructure.Utils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrandGuardAI.Tests;

public class EndToEndWorkflowIntegrationTests
{
    private (ApplicationDbContext Context, GuidelineService GuidelineSvc, ContentSubmissionService ContentSvc, ValidationOrchestrator Orchestrator, ReviewService ReviewSvc) SetupServices()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();

        var pdfExtractor = new PdfPigTextExtractor(NullLogger<PdfPigTextExtractor>.Instance);
        var chunker = new DocumentChunker();
        var embeddingSvc = new EmbeddingService(httpClient, config, NullLogger<EmbeddingService>.Instance);
        var searchSvc = new SemanticSearchService(context, embeddingSvc, NullLogger<SemanticSearchService>.Instance);
        var ruleEngine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var llmSvc = new LlmValidationService(httpClient, config, NullLogger<LlmValidationService>.Instance);

        var guidelineSvc = new GuidelineService(context, pdfExtractor, chunker, embeddingSvc, NullLogger<GuidelineService>.Instance);
        var contentSvc = new ContentSubmissionService(context, NullLogger<ContentSubmissionService>.Instance);
        var orchestrator = new ValidationOrchestrator(context, ruleEngine, searchSvc, llmSvc, NullLogger<ValidationOrchestrator>.Instance);
        var reviewSvc = new ReviewService(context, NullLogger<ReviewService>.Instance);

        return (context, guidelineSvc, contentSvc, orchestrator, reviewSvc);
    }

    [Fact]
    public async Task CompleteEndToEndCoreWorkflow_ShouldSucceed()
    {
        var (context, guidelineSvc, contentSvc, orchestrator, reviewSvc) = SetupServices();

        // 1. Upload sample brand guideline PDF
        var pdfBytes = SamplePdfGenerator.CreateSampleGuidelinePdf();
        using var pdfStream = new MemoryStream(pdfBytes);

        var uploadedGuideline = await guidelineSvc.UploadGuidelineAsync(
            "Acme Master Brand Guidelines",
            "Acme_Brand_Guidelines.pdf",
            pdfStream);

        uploadedGuideline.Should().NotBeNull();
        uploadedGuideline.Status.Should().Be(GuidelineStatus.Ready);
        uploadedGuideline.ChunkCount.Should().BeGreaterThan(0);

        // 2. Submit non-compliant marketing content
        var submission = await contentSvc.CreateSubmissionAsync(new CreateContentSubmissionDto(
            "Global Launch Headline",
            "Social Media Post",
            "Our revolutionary solution guarantees 100% results and is the best solution in the world!"
        ));

        submission.Should().NotBeNull();
        submission.Status.Should().Be(ContentSubmissionStatus.Draft);

        // 3. Execute Hybrid Validation (Semantic Retrieval + Deterministic Rules + LLM + Evidence Verification)
        var validationResult = await orchestrator.RunValidationAsync(submission.Id);

        validationResult.Should().NotBeNull();
        validationResult.Status.Should().Be("Completed");
        validationResult.OverallRiskLevel.Should().Be(RiskSeverity.High);
        validationResult.TotalFindings.Should().BeGreaterThanOrEqualTo(2);
        validationResult.HighRiskCount.Should().BeGreaterThanOrEqualTo(1);

        // Verify that evidence is present and verified
        var guaranteeFinding = validationResult.Findings.FirstOrDefault(f =>
            f.IssueType.Contains("Guarantee", StringComparison.OrdinalIgnoreCase) ||
            f.OriginalText.Contains("guarantee", StringComparison.OrdinalIgnoreCase));

        guaranteeFinding.Should().NotBeNull();
        guaranteeFinding!.Severity.Should().Be(RiskSeverity.High);
        guaranteeFinding.EvidenceText.Should().NotBeNullOrWhiteSpace();

        // Verify content status updated to PendingReview
        var updatedSubmission = await contentSvc.GetSubmissionByIdAsync(submission.Id);
        updatedSubmission!.Status.Should().Be(ContentSubmissionStatus.PendingReview);

        // 4. Human Reviewer Decision: Request Changes
        var reviewResult = await reviewSvc.SubmitDecisionAsync(new SubmitReviewDecisionDto(
            submission.Id,
            validationResult.ValidationRunId,
            "Atharv Khot (Principal Reviewer)",
            ReviewDecisionType.ChangesRequested,
            "Absolute guarantee violates Policy 2.1. Please rephrase to describe verified customer outcomes."
        ));

        reviewResult.Should().NotBeNull();
        reviewResult.Decision.Should().Be(ReviewDecisionType.ChangesRequested);

        // 5. Verify review history persistence
        var history = await reviewSvc.GetReviewHistoryAsync();
        history.Should().HaveCount(1);
        history[0].ContentSubmissionId.Should().Be(submission.Id);
        history[0].Decision.Should().Be(ReviewDecisionType.ChangesRequested);
        history[0].OverallRiskLevel.Should().Be(RiskSeverity.High);

        // 6. Verify final content submission status reflects decision
        var finalSubmission = await contentSvc.GetSubmissionByIdAsync(submission.Id);
        finalSubmission!.Status.Should().Be(ContentSubmissionStatus.ChangesRequested);
        finalSubmission.Decisions.Should().HaveCount(1);
    }
}
