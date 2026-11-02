using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Data;
using BrandGuardAI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrandGuardAI.Tests;

public class AdaptiveDualModeReviewTests
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

    [Fact]
    public async Task GuidedReviewDecision_ShouldSaveDirectlyAndUpdateSubmission()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var submission = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Solar Energy Hero Campaign",
            ContentText = "100% clean power with verified carbon offsets.",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var request = new SubmitReviewDecisionDto(
            submission.Id,
            null,
            "Senior Brand Manager",
            ReviewDecisionType.Approved,
            "Claims verified against green guidelines."
        );

        // Act
        var result = await service.SubmitDecisionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be(ReviewDecisionType.Approved);
        result.ReviewerName.Should().Be("Senior Brand Manager");

        var updatedSub = await context.ContentSubmissions.FindAsync(submission.Id);
        updatedSub!.Status.Should().Be(ContentSubmissionStatus.Approved);
    }

    [Fact]
    public async Task DuplicateDecision_WithinWindow_ShouldReturnExistingWithoutDuplicateEntity()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var submission = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Duplicate Test Submission",
            ContentText = "Check duplicate prevention.",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var request = new SubmitReviewDecisionDto(
            submission.Id,
            null,
            "Reviewer A",
            ReviewDecisionType.ChangesRequested,
            "Initial comments"
        );

        // Act - First submission
        var first = await service.SubmitDecisionAsync(request);

        // Act - Duplicate immediate submission
        var second = await service.SubmitDecisionAsync(request);

        // Assert
        second.Id.Should().Be(first.Id);
        var totalDecisions = await context.ReviewDecisions.CountAsync(d => d.ContentSubmissionId == submission.Id);
        totalDecisions.Should().Be(1);
    }

    [Fact]
    public async Task BatchFindingAcknowledgment_ShouldPersistInDatabase()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var runId = Guid.NewGuid();
        var finding1 = new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = runId,
            IssueType = "Superlative Claim",
            Severity = RiskSeverity.High,
            OriginalText = "the best solution in the world",
            Explanation = "Unsubstantiated superlative",
            SuggestedFix = "leading industry solution",
            IsAcknowledged = false
        };
        var finding2 = new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = runId,
            IssueType = "Absolute Guarantee",
            Severity = RiskSeverity.High,
            OriginalText = "guarantees 100% results",
            Explanation = "Absolute outcome claim",
            SuggestedFix = "designed to optimize results",
            IsAcknowledged = false
        };
        context.ValidationFindings.AddRange(finding1, finding2);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var ackRequest = new AcknowledgeFindingsRequest(
            new List<Guid> { finding1.Id, finding2.Id },
            "Editorial Reviewer"
        );

        // Act
        var result = await service.AcknowledgeFindingsAsync(ackRequest);

        // Assert
        result.AcknowledgedCount.Should().Be(2);
        result.AcknowledgedFindingIds.Should().Contain(finding1.Id);
        result.AcknowledgedFindingIds.Should().Contain(finding2.Id);

        var refreshedF1 = await context.ValidationFindings.FindAsync(finding1.Id);
        refreshedF1!.IsAcknowledged.Should().BeTrue();
        refreshedF1.AcknowledgedBy.Should().Be("Editorial Reviewer");
        refreshedF1.AcknowledgedAt.Should().NotBeNull();

        var refreshedF2 = await context.ValidationFindings.FindAsync(finding2.Id);
        refreshedF2!.IsAcknowledged.Should().BeTrue();
        refreshedF2.AcknowledgedBy.Should().Be("Editorial Reviewer");
        refreshedF2.AcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BatchSubmissionReview_ShouldApproveAndRejectMultipleSubmissions()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sub1 = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Low Risk Social Ad",
            ContentText = "Fresh coffee daily.",
            Status = ContentSubmissionStatus.PendingReview
        };
        var run1 = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub1.Id,
            OverallRiskLevel = RiskSeverity.Low,
            Status = "Completed"
        };
        var sub2 = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Medium Risk Blog",
            ContentText = "Leading platform in town.",
            Status = ContentSubmissionStatus.PendingReview
        };
        var run2 = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub2.Id,
            OverallRiskLevel = RiskSeverity.Medium,
            Status = "Completed"
        };

        context.ContentSubmissions.AddRange(sub1, sub2);
        context.ValidationRuns.AddRange(run1, run2);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var batchDto = new BatchReviewDecisionDto(
            new List<Guid> { sub1.Id, sub2.Id },
            ReviewDecisionType.Approved,
            "Lead Reviewer",
            "Batch approved low and medium risk items."
        );

        // Act
        var result = await service.SubmitBatchDecisionsAsync(batchDto);

        // Assert
        result.TotalProcessed.Should().Be(2);
        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);

        var refreshedSub1 = await context.ContentSubmissions.FindAsync(sub1.Id);
        refreshedSub1!.Status.Should().Be(ContentSubmissionStatus.Approved);

        var refreshedSub2 = await context.ContentSubmissions.FindAsync(sub2.Id);
        refreshedSub2!.Status.Should().Be(ContentSubmissionStatus.Approved);
    }

    [Fact]
    public async Task BatchSubmissionReview_HighRiskWithoutConfirmation_ShouldBlockApproval()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var highRiskSub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "High Risk Guarantees Post",
            ContentText = "100% guaranteed miracle cures.",
            Status = ContentSubmissionStatus.PendingReview
        };
        var highRiskRun = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = highRiskSub.Id,
            OverallRiskLevel = RiskSeverity.High,
            Status = "Completed"
        };

        var lowRiskSub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Safe Post",
            ContentText = "Our friendly team welcomes you.",
            Status = ContentSubmissionStatus.PendingReview
        };
        var lowRiskRun = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = lowRiskSub.Id,
            OverallRiskLevel = RiskSeverity.Low,
            Status = "Completed"
        };

        context.ContentSubmissions.AddRange(highRiskSub, lowRiskSub);
        context.ValidationRuns.AddRange(highRiskRun, lowRiskRun);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);

        // Act: Attempt to batch-approve without ConfirmHighRiskApproval
        var batchDto = new BatchReviewDecisionDto(
            new List<Guid> { highRiskSub.Id, lowRiskSub.Id },
            ReviewDecisionType.Approved,
            "Brand Auditor",
            "Bulk approval attempt",
            ConfirmHighRiskApproval: false
        );

        var result = await service.SubmitBatchDecisionsAsync(batchDto);

        // Assert: Low risk item succeeds, high risk item is blocked
        result.TotalProcessed.Should().Be(2);
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);

        var highRiskResult = result.Results.First(r => r.ContentSubmissionId == highRiskSub.Id);
        highRiskResult.Success.Should().BeFalse();
        highRiskResult.ErrorMessage.Should().Contain("High Risk");

        var refreshedHigh = await context.ContentSubmissions.FindAsync(highRiskSub.Id);
        refreshedHigh!.Status.Should().Be(ContentSubmissionStatus.PendingReview); // Unchanged

        var refreshedLow = await context.ContentSubmissions.FindAsync(lowRiskSub.Id);
        refreshedLow!.Status.Should().Be(ContentSubmissionStatus.Approved);
    }

    [Fact]
    public async Task BatchSubmissionReview_HighRiskWithExplicitConfirmation_ShouldSucceed()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var highRiskSub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Legal Exception Campaign",
            ContentText = "Special pre-approved claim with signed disclaimer.",
            Status = ContentSubmissionStatus.PendingReview
        };
        var highRiskRun = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = highRiskSub.Id,
            OverallRiskLevel = RiskSeverity.High,
            Status = "Completed"
        };
        context.ContentSubmissions.Add(highRiskSub);
        context.ValidationRuns.Add(highRiskRun);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);

        // Act: ConfirmHighRiskApproval = true
        var batchDto = new BatchReviewDecisionDto(
            new List<Guid> { highRiskSub.Id },
            ReviewDecisionType.Approved,
            "General Counsel",
            "Exception authorized under policy appendix B.",
            ConfirmHighRiskApproval: true
        );

        var result = await service.SubmitBatchDecisionsAsync(batchDto);

        // Assert
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(0);

        var refreshedSub = await context.ContentSubmissions.FindAsync(highRiskSub.Id);
        refreshedSub!.Status.Should().Be(ContentSubmissionStatus.Approved);
    }

    [Fact]
    public async Task BatchSubmissionReview_MissingSubmissionIds_ShouldReportPartialFailure()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var validSub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Valid Submission",
            ContentText = "Clean copy.",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(validSub);
        await context.SaveChangesAsync();

        var nonExistentId = Guid.NewGuid();
        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var batchDto = new BatchReviewDecisionDto(
            new List<Guid> { validSub.Id, nonExistentId },
            ReviewDecisionType.Rejected,
            "Auditor",
            "Review note"
        );

        // Act
        var result = await service.SubmitBatchDecisionsAsync(batchDto);

        // Assert
        result.TotalProcessed.Should().Be(2);
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);

        var failedResult = result.Results.First(r => r.ContentSubmissionId == nonExistentId);
        failedResult.Success.Should().BeFalse();
        failedResult.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task BatchSubmissionReview_InvalidReviewer_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var batchDto = new BatchReviewDecisionDto(
            new List<Guid> { Guid.NewGuid() },
            ReviewDecisionType.Approved,
            "",
            "Comments"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitBatchDecisionsAsync(batchDto));
    }

    [Fact]
    public async Task ReviewHistory_ShouldReflectBatchDecisionsConsistently()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "History Consistency Check",
            ContentType = "WebsiteCopy",
            ContentText = "Text.",
            Status = ContentSubmissionStatus.PendingReview
        };
        var run = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub.Id,
            OverallRiskLevel = RiskSeverity.Medium,
            TotalFindings = 2,
            Status = "Completed"
        };
        context.ContentSubmissions.Add(sub);
        context.ValidationRuns.Add(run);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var batchDto = new BatchReviewDecisionDto(
            new List<Guid> { sub.Id },
            ReviewDecisionType.ChangesRequested,
            "History Reviewer",
            "Revision required."
        );
        await service.SubmitBatchDecisionsAsync(batchDto);

        // Act
        var history = await service.GetReviewHistoryAsync();

        // Assert
        history.Should().NotBeEmpty();
        var item = history.First(h => h.ContentSubmissionId == sub.Id);
        item.Decision.Should().Be(ReviewDecisionType.ChangesRequested);
        item.ReviewerName.Should().Be("History Reviewer");
        item.OverallRiskLevel.Should().Be(RiskSeverity.Medium);
        item.TotalFindings.Should().Be(2);
    }
}
