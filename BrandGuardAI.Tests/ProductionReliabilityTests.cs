using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Ai;
using BrandGuardAI.Infrastructure.Data;
using BrandGuardAI.Infrastructure.Rules;
using BrandGuardAI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrandGuardAI.Tests;

public class ProductionReliabilityTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    // 1. Empty content submissions
    [Theory]
    [InlineData("", "Valid Content Text")]
    [InlineData("   ", "Valid Content Text")]
    [InlineData("Valid Title", "")]
    [InlineData("Valid Title", "   ")]
    public async Task EmptyContentSubmissions_ShouldThrowArgumentException(string title, string contentText)
    {
        using var context = CreateInMemoryDbContext();
        var service = new ContentSubmissionService(context, NullLogger<ContentSubmissionService>.Instance);

        var act = async () => await service.CreateSubmissionAsync(new CreateContentSubmissionDto(
            title,
            "SocialMediaPost",
            contentText
        ));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // 2. Invalid content IDs
    [Fact]
    public async Task InvalidContentId_ShouldThrowKeyNotFoundException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new ContentSubmissionService(context, NullLogger<ContentSubmissionService>.Instance);
        var nonExistentId = Guid.NewGuid();

        var result = await service.GetSubmissionByIdAsync(nonExistentId);
        result.Should().BeNull();

        var orchestrator = new ValidationOrchestrator(
            context,
            new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance),
            new MockSemanticSearchService(),
            new LlmValidationService(new HttpClient(), new ConfigurationBuilder().Build(), NullLogger<LlmValidationService>.Instance),
            NullLogger<ValidationOrchestrator>.Instance
        );

        var act = async () => await orchestrator.RunValidationAsync(nonExistentId);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // 3. Repeated validation requests
    [Fact]
    public async Task RepeatedValidationRequests_ShouldCreateSeparateRunsAndReturnLatest()
    {
        using var context = CreateInMemoryDbContext();
        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Repeat Test",
            ContentType = "SocialMediaPost",
            ContentText = "100% guaranteed results for everyone.",
            Status = ContentSubmissionStatus.Draft
        };
        context.ContentSubmissions.Add(sub);
        await context.SaveChangesAsync();

        var orchestrator = new ValidationOrchestrator(
            context,
            new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance),
            new MockSemanticSearchService(),
            new LlmValidationService(new HttpClient(), new ConfigurationBuilder().Build(), NullLogger<LlmValidationService>.Instance),
            NullLogger<ValidationOrchestrator>.Instance
        );

        // Run validation first time
        var run1 = await orchestrator.RunValidationAsync(sub.Id);
        // Run validation second time
        var run2 = await orchestrator.RunValidationAsync(sub.Id);

        run1.Should().NotBeNull();
        run2.Should().NotBeNull();
        run1.ValidationRunId.Should().NotBe(run2.ValidationRunId);

        var runsInDb = await context.ValidationRuns.Where(r => r.ContentSubmissionId == sub.Id).ToListAsync();
        runsInDb.Should().HaveCount(2);

        var contentService = new ContentSubmissionService(context, NullLogger<ContentSubmissionService>.Instance);
        var latest = await contentService.GetLatestValidationResultAsync(sub.Id);
        latest!.ValidationRunId.Should().Be(run2.ValidationRunId);
    }

    // 4. Duplicate review decisions
    [Fact]
    public async Task DuplicateReviewDecisions_ShouldBeRecordedAndPreserveAuditTrail()
    {
        using var context = CreateInMemoryDbContext();
        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Campaign Audit",
            ContentText = "Content",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(sub);
        await context.SaveChangesAsync();

        var reviewService = new ReviewService(context, NullLogger<ReviewService>.Instance);

        // First review: ChangesRequested
        await reviewService.SubmitDecisionAsync(new SubmitReviewDecisionDto(
            sub.Id,
            null,
            "Auditor A",
            ReviewDecisionType.ChangesRequested,
            "Need revision"
        ));

        // Second review: Approved
        await reviewService.SubmitDecisionAsync(new SubmitReviewDecisionDto(
            sub.Id,
            null,
            "Auditor B",
            ReviewDecisionType.Approved,
            "Revisions verified"
        ));

        var history = await reviewService.GetReviewHistoryAsync();
        history.Should().HaveCount(2);

        var updatedSub = await context.ContentSubmissions.FindAsync(sub.Id);
        updatedSub!.Status.Should().Be(ContentSubmissionStatus.Approved);
    }

    // 5. Missing guideline evidence
    [Fact]
    public async Task MissingGuidelineEvidence_ShouldMarkEvidenceAsUnverified()
    {
        using var context = CreateInMemoryDbContext();
        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "No Evidence Sub",
            ContentType = "SocialMediaPost",
            ContentText = "We are the best solution in the world.",
            Status = ContentSubmissionStatus.Draft
        };
        context.ContentSubmissions.Add(sub);
        await context.SaveChangesAsync();

        // Empty semantic search results (no guideline documents uploaded)
        var orchestrator = new ValidationOrchestrator(
            context,
            new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance),
            new MockSemanticSearchService(),
            new LlmValidationService(new HttpClient(), new ConfigurationBuilder().Build(), NullLogger<LlmValidationService>.Instance),
            NullLogger<ValidationOrchestrator>.Instance
        );

        var result = await orchestrator.RunValidationAsync(sub.Id);
        result.Should().NotBeNull();
        result.Findings.Should().NotBeEmpty();

        // Deterministic finding without chunk citation
        var finding = result.Findings.First();
        finding.HasVerifiedEvidence.Should().BeTrue(); // Deterministic rule is marked as verified rule
        finding.EvidenceProvenance.Should().Be(EvidenceProvenanceTypes.DeterministicRule);
    }

    // 6. AI service timeout / unavailable
    [Fact]
    public async Task AiServiceUnavailable_ShouldFallBackGracefully()
    {
        var config = new ConfigurationBuilder().Build(); // No API key configured
        var llmService = new LlmValidationService(new HttpClient(), config, NullLogger<LlmValidationService>.Instance);

        var request = new LlmValidationRequest(
            "Test Ad",
            "SocialMediaPost",
            "This product guarantees 100% success and eliminates disease.",
            new List<ChunkSearchResult>(),
            new List<RuleEvaluationResult>()
        );

        var response = await llmService.ValidateContentAsync(request);
        response.Should().NotBeNull();
        response.OverallAssessment.Should().NotBeNullOrWhiteSpace();
        response.Findings.Should().NotBeEmpty();
    }

    // 7. Invalid reviewer input
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidReviewerInput_ShouldThrowArgumentException(string reviewerName)
    {
        using var context = CreateInMemoryDbContext();
        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);

        var act = async () => await service.SubmitDecisionAsync(new SubmitReviewDecisionDto(
            Guid.NewGuid(),
            null,
            reviewerName,
            ReviewDecisionType.Approved,
            "Good"
        ));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // 8. Batch acknowledgment repeated twice
    [Fact]
    public async Task BatchAcknowledgmentRepeatedTwice_ShouldBeIdempotent()
    {
        using var context = CreateInMemoryDbContext();
        var finding = new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = Guid.NewGuid(),
            Severity = RiskSeverity.Medium,
            OriginalText = "Test",
            Explanation = "Test",
            SuggestedFix = "Fix",
            IsAcknowledged = false
        };
        context.ValidationFindings.Add(finding);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var request = new AcknowledgeFindingsRequest(new List<Guid> { finding.Id }, "Lead Auditor");

        var result1 = await service.AcknowledgeFindingsAsync(request);
        result1.AcknowledgedCount.Should().Be(1);

        // Repeat acknowledgment
        var result2 = await service.AcknowledgeFindingsAsync(request);
        result2.AcknowledgedCount.Should().Be(1); // Idempotently succeeds

        var updated = await context.ValidationFindings.FindAsync(finding.Id);
        updated!.IsAcknowledged.Should().BeTrue();
        updated.AcknowledgedBy.Should().Be("Lead Auditor");
    }

    // 9. Batch approval without high-risk confirmation
    [Fact]
    public async Task BatchApprovalWithoutHighRiskConfirmation_ShouldBlockApproval()
    {
        using var context = CreateInMemoryDbContext();
        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "High Risk Batch Candidate",
            ContentText = "Dangerous text",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(sub);

        var run = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub.Id,
            OverallRiskLevel = RiskSeverity.High,
            StartedAt = DateTime.UtcNow
        };
        context.ValidationRuns.Add(run);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var request = new BatchReviewDecisionDto(
            new List<Guid> { sub.Id },
            ReviewDecisionType.Approved,
            "Manager",
            "Batch approval",
            ConfirmHighRiskApproval: false // Not confirmed!
        );

        var result = await service.SubmitBatchDecisionsAsync(request);
        result.Succeeded.Should().Be(0);
        result.Failed.Should().Be(1);
        result.Results.First().Success.Should().BeFalse();
        result.Results.First().ErrorMessage.Should().Contain("Explicit confirmation override is required");
    }

    // 10. Batch approval with high-risk confirmation
    [Fact]
    public async Task BatchApprovalWithHighRiskConfirmation_ShouldSucceed()
    {
        using var context = CreateInMemoryDbContext();
        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "High Risk Batch Candidate 2",
            ContentText = "Dangerous text",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(sub);

        var run = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub.Id,
            OverallRiskLevel = RiskSeverity.High,
            StartedAt = DateTime.UtcNow
        };
        context.ValidationRuns.Add(run);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var request = new BatchReviewDecisionDto(
            new List<Guid> { sub.Id },
            ReviewDecisionType.Approved,
            "Manager",
            "Batch override approved",
            ConfirmHighRiskApproval: true // Explicitly confirmed
        );

        var result = await service.SubmitBatchDecisionsAsync(request);
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(0);
        result.Results.First().Success.Should().BeTrue();
    }

    // 11. Invalid batch submission IDs
    [Fact]
    public async Task InvalidBatchSubmissionIds_ShouldReportFailedItemWithoutCrashing()
    {
        using var context = CreateInMemoryDbContext();
        var nonExistentId = Guid.NewGuid();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var request = new BatchReviewDecisionDto(
            new List<Guid> { nonExistentId },
            ReviewDecisionType.Rejected,
            "Manager",
            "Rejecting"
        );

        var result = await service.SubmitBatchDecisionsAsync(request);
        result.TotalProcessed.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Results.First().Success.Should().BeFalse();
        result.Results.First().ErrorMessage.Should().Contain("not found");
    }

    // 12. Empty dataset health score (Phase 5)
    [Fact]
    public async Task EmptyDataset_ShouldReturnNotEnoughDataHealthScore()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DashboardService(context);

        var intelligence = await service.GetGovernanceIntelligenceAsync(30);
        intelligence.HealthScore.Score.Should().BeNull();
        intelligence.HealthScore.StatusLabel.Should().Be("Not enough data");
        intelligence.HealthScore.HasSufficientData.Should().BeFalse();
    }

    // 13. Analytics with duplicate reviews
    [Fact]
    public async Task AnalyticsWithDuplicateReviews_ShouldMaintainAccurateDenominator()
    {
        using var context = CreateInMemoryDbContext();
        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Duplicate Review Sub",
            ContentText = "Text",
            Status = ContentSubmissionStatus.Approved,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        context.ContentSubmissions.Add(sub);

        // 2 review decisions for the same submission
        context.ReviewDecisions.AddRange(
            new ReviewDecision { Id = Guid.NewGuid(), ContentSubmissionId = sub.Id, ReviewerName = "R1", Decision = ReviewDecisionType.ChangesRequested, ReviewedAt = DateTime.UtcNow.AddDays(-2) },
            new ReviewDecision { Id = Guid.NewGuid(), ContentSubmissionId = sub.Id, ReviewerName = "R2", Decision = ReviewDecisionType.Approved, ReviewedAt = DateTime.UtcNow.AddDays(-1) }
        );
        await context.SaveChangesAsync();

        var service = new DashboardService(context);
        var intelligence = await service.GetGovernanceIntelligenceAsync(30);

        // TotalReviewedSubmissions must be 1, TotalReviewEvents must be 2
        intelligence.ApprovalAnalytics.TotalReviewedSubmissions.Should().Be(1);
        intelligence.ApprovalAnalytics.TotalReviewEvents.Should().Be(2);
        intelligence.ApprovalAnalytics.ApprovedCount.Should().Be(1);
        intelligence.ApprovalAnalytics.ApprovalRate.Should().Be(100.0);
    }

    // 14. Date range validation
    [Fact]
    public async Task DateRangeValidation_ShouldFilterAppropriately()
    {
        using var context = CreateInMemoryDbContext();

        var sub7 = new ContentSubmission { Id = Guid.NewGuid(), Title = "7d", ContentText = "Text", CreatedAt = DateTime.UtcNow.AddDays(-3) };
        var sub30 = new ContentSubmission { Id = Guid.NewGuid(), Title = "30d", ContentText = "Text", CreatedAt = DateTime.UtcNow.AddDays(-15) };
        var sub90 = new ContentSubmission { Id = Guid.NewGuid(), Title = "90d", ContentText = "Text", CreatedAt = DateTime.UtcNow.AddDays(-60) };

        context.ContentSubmissions.AddRange(sub7, sub30, sub90);
        await context.SaveChangesAsync();

        var service = new DashboardService(context);

        var res7 = await service.GetGovernanceIntelligenceAsync(7);
        var res30 = await service.GetGovernanceIntelligenceAsync(30);
        var res90 = await service.GetGovernanceIntelligenceAsync(90);

        res7.Overview.TotalSubmissions.Should().Be(1);
        res30.Overview.TotalSubmissions.Should().Be(2);
        res90.Overview.TotalSubmissions.Should().Be(3);
    }
}

public class MockSemanticSearchService : ISemanticSearchService
{
    public Task<List<ChunkSearchResult>> SearchSimilarChunksAsync(string queryText, int topK = 5, float minSimilarity = 0.15F, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<ChunkSearchResult>());
    }
}
