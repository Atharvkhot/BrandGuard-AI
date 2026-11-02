using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Data;
using BrandGuardAI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BrandGuardAI.Tests;

public class GovernanceIntelligenceTests
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
    public async Task EmptyDatabase_ShouldReturnDefaultMetrics_WithoutExceptionsOrNaN()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new DashboardService(context);

        // Act
        var intelligence = await service.GetGovernanceIntelligenceAsync(30);

        // Assert
        intelligence.Should().NotBeNull();
        intelligence.SelectedDays.Should().Be(30);

        // Overview
        intelligence.Overview.TotalSubmissions.Should().Be(0);
        intelligence.Overview.TotalValidationRuns.Should().Be(0);
        intelligence.Overview.TotalFindings.Should().Be(0);
        intelligence.Overview.HighRiskFindings.Should().Be(0);
        intelligence.Overview.MediumRiskFindings.Should().Be(0);
        intelligence.Overview.LowRiskFindings.Should().Be(0);

        // Approval Analytics
        intelligence.ApprovalAnalytics.TotalReviewedSubmissions.Should().Be(0);
        intelligence.ApprovalAnalytics.TotalReviewEvents.Should().Be(0);
        intelligence.ApprovalAnalytics.ApprovalRate.Should().Be(0.0);
        intelligence.ApprovalAnalytics.RejectionRate.Should().Be(0.0);
        intelligence.ApprovalAnalytics.ChangesRequestedRate.Should().Be(0.0);

        // Health Score (Phase 5: Nullable score with 'Not enough data' for empty datasets)
        intelligence.HealthScore.Score.Should().BeNull();
        intelligence.HealthScore.StatusLabel.Should().Be("Not enough data");
        intelligence.HealthScore.HasSufficientData.Should().BeFalse();
        intelligence.HealthScore.FormulaExplanation.Should().NotBeNullOrWhiteSpace();

        // Trends: exactly 30 days, all zeroes
        intelligence.ViolationTrends.Should().HaveCount(30);
        intelligence.ViolationTrends.All(t => t.HighRiskCount == 0 && t.MediumRiskCount == 0 && t.LowRiskCount == 0 && t.TotalFindings == 0).Should().BeTrue();

        // Top Guidelines
        intelligence.TopViolatedGuidelines.Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovalAndRejectionRates_WithDuplicateReviews_ShouldUseAccurateSubmissionDenominator()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var sub1 = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Campaign 1",
            ContentText = "Text 1",
            Status = ContentSubmissionStatus.Approved,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var sub2 = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Campaign 2",
            ContentText = "Text 2",
            Status = ContentSubmissionStatus.Rejected,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };
        var sub3 = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Campaign 3",
            ContentText = "Text 3",
            Status = ContentSubmissionStatus.PendingReview,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        context.ContentSubmissions.AddRange(sub1, sub2, sub3);

        // Sub 1 has TWO review decisions (first ChangesRequested, later Approved)
        var review1a = new ReviewDecision
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub1.Id,
            ReviewerName = "Reviewer 1",
            Decision = ReviewDecisionType.ChangesRequested,
            Comments = "Fix disclaimer",
            ReviewedAt = DateTime.UtcNow.AddDays(-2)
        };
        var review1b = new ReviewDecision
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub1.Id,
            ReviewerName = "Reviewer 2",
            Decision = ReviewDecisionType.Approved,
            Comments = "Looks good now",
            ReviewedAt = DateTime.UtcNow.AddDays(-1)
        };

        // Sub 2 has ONE review decision (Rejected)
        var review2 = new ReviewDecision
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub2.Id,
            ReviewerName = "Reviewer 1",
            Decision = ReviewDecisionType.Rejected,
            Comments = "Unsubstantiated claims",
            ReviewedAt = DateTime.UtcNow.AddDays(-3)
        };

        context.ReviewDecisions.AddRange(review1a, review1b, review2);
        await context.SaveChangesAsync();

        var service = new DashboardService(context);

        // Act
        var result = await service.GetGovernanceIntelligenceAsync(30);

        // Assert
        // Total reviewed submissions must be 2 (sub1 and sub2), NOT 3
        result.ApprovalAnalytics.TotalReviewedSubmissions.Should().Be(2);
        result.ApprovalAnalytics.TotalReviewEvents.Should().Be(3);
        result.ApprovalAnalytics.ApprovedCount.Should().Be(1);
        result.ApprovalAnalytics.RejectedCount.Should().Be(1);
        result.ApprovalAnalytics.ChangesRequestedCount.Should().Be(0);

        // Rates based on unique submissions: 1/2 = 50%
        result.ApprovalAnalytics.ApprovalRate.Should().Be(50.0);
        result.ApprovalAnalytics.RejectionRate.Should().Be(50.0);
        result.ApprovalAnalytics.ChangesRequestedRate.Should().Be(0.0);
    }

    [Fact]
    public async Task RiskCounts_HighMediumLow_ShouldAggregateAccurately()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Ad Test",
            ContentText = "Content",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        context.ContentSubmissions.Add(sub);

        var run = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub.Id,
            StartedAt = DateTime.UtcNow.AddDays(-5),
            Status = "Completed"
        };
        context.ValidationRuns.Add(run);

        context.ValidationFindings.AddRange(
            new ValidationFinding { Id = Guid.NewGuid(), ValidationRunId = run.Id, Severity = RiskSeverity.High, Explanation = "High 1", CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new ValidationFinding { Id = Guid.NewGuid(), ValidationRunId = run.Id, Severity = RiskSeverity.High, Explanation = "High 2", CreatedAt = DateTime.UtcNow.AddDays(-4) },
            new ValidationFinding { Id = Guid.NewGuid(), ValidationRunId = run.Id, Severity = RiskSeverity.Medium, Explanation = "Med 1", CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new ValidationFinding { Id = Guid.NewGuid(), ValidationRunId = run.Id, Severity = RiskSeverity.Low, Explanation = "Low 1", CreatedAt = DateTime.UtcNow.AddDays(-2) }
        );

        await context.SaveChangesAsync();

        var service = new DashboardService(context);

        // Act
        var result = await service.GetGovernanceIntelligenceAsync(30);

        // Assert
        result.Overview.TotalFindings.Should().Be(4);
        result.Overview.HighRiskFindings.Should().Be(2);
        result.Overview.MediumRiskFindings.Should().Be(1);
        result.Overview.LowRiskFindings.Should().Be(1);
    }

    [Fact]
    public async Task DailyViolationTrends_ShouldIncludeAllZeroActivityDates()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var sub = new ContentSubmission { Id = Guid.NewGuid(), Title = "Trends Test", ContentText = "Text", CreatedAt = DateTime.UtcNow.AddDays(-3) };
        context.ContentSubmissions.Add(sub);

        var run = new ValidationRun { Id = Guid.NewGuid(), ContentSubmissionId = sub.Id, StartedAt = DateTime.UtcNow.AddDays(-3) };
        context.ValidationRuns.Add(run);

        // Only 1 finding 3 days ago
        context.ValidationFindings.Add(new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = run.Id,
            Severity = RiskSeverity.High,
            Explanation = "Violation 3 days ago",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        });
        await context.SaveChangesAsync();

        var service = new DashboardService(context);

        // Act
        var result = await service.GetGovernanceIntelligenceAsync(7);

        // Assert
        result.ViolationTrends.Should().HaveCount(7);
        // At least 6 days should have 0 total findings
        result.ViolationTrends.Count(d => d.TotalFindings == 0).Should().Be(6);
        result.ViolationTrends.Count(d => d.TotalFindings == 1).Should().Be(1);

        // Ensure chronological ascending order
        for (int i = 0; i < result.ViolationTrends.Count - 1; i++)
        {
            DateTime.Parse(result.ViolationTrends[i].Date).Should().BeBefore(DateTime.Parse(result.ViolationTrends[i + 1].Date));
        }
    }

    [Fact]
    public async Task TopViolatedGuidelines_ShouldDistinguishVerifiedGuidelineFromDeterministicAndHeuristicAI()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var guideline = new BrandGuideline
        {
            Id = Guid.NewGuid(),
            Title = "FTC Environmental Claims Policy",
            UploadedAt = DateTime.UtcNow
        };
        context.BrandGuidelines.Add(guideline);

        var chunk = new GuidelineChunk
        {
            Id = Guid.NewGuid(),
            BrandGuidelineId = guideline.Id,
            BrandGuideline = guideline,
            ChunkIndex = 1,
            ChunkText = "Carbon neutral claims require 3rd party certification."
        };
        context.GuidelineChunks.Add(chunk);

        var sub = new ContentSubmission { Id = Guid.NewGuid(), Title = "Claims Test", ContentText = "Text", CreatedAt = DateTime.UtcNow.AddDays(-2) };
        context.ContentSubmissions.Add(sub);

        var run = new ValidationRun { Id = Guid.NewGuid(), ContentSubmissionId = sub.Id, StartedAt = DateTime.UtcNow.AddDays(-2) };
        context.ValidationRuns.Add(run);

        // Finding 1 & 2: Verified guideline citations
        context.ValidationFindings.Add(new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = run.Id,
            GuidelineChunkId = chunk.Id,
            Severity = RiskSeverity.High,
            Explanation = "Unverified green claim",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
        context.ValidationFindings.Add(new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = run.Id,
            GuidelineChunkId = chunk.Id,
            Severity = RiskSeverity.Medium,
            Explanation = "Second green claim issue",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });

        // Finding 3: Deterministic Rule (No chunk, but IssueType denotes Rule)
        context.ValidationFindings.Add(new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = run.Id,
            IssueType = "Guaranteed Returns Prohibition",
            Severity = RiskSeverity.High,
            Explanation = "Financial claim",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });

        // Finding 4: Unverified AI detection (No chunk, empty IssueType)
        context.ValidationFindings.Add(new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = run.Id,
            IssueType = "",
            Severity = RiskSeverity.Low,
            Explanation = "Heuristic AI detected aggressive tone",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });

        // Add deterministic rule to context
        context.ValidationRules.Add(new ValidationRule
        {
            Id = Guid.NewGuid(),
            RuleName = "Guaranteed Returns Prohibition",
            IsActive = true
        });

        await context.SaveChangesAsync();

        var service = new DashboardService(context);

        // Act
        var result = await service.GetGovernanceIntelligenceAsync(30);

        // Assert
        result.TopViolatedGuidelines.Should().HaveCount(3);

        // Verified Guideline
        var verifiedItem = result.TopViolatedGuidelines.FirstOrDefault(g => g.IsVerifiedGuideline);
        verifiedItem.Should().NotBeNull();
        verifiedItem!.GuidelineOrRuleName.Should().Contain("FTC Environmental Claims Policy");
        verifiedItem.ViolationCount.Should().Be(2);
        verifiedItem.HighRiskCount.Should().Be(1);
        verifiedItem.MediumRiskCount.Should().Be(1);
        verifiedItem.PercentageOfTotal.Should().Be(50.0); // 2 out of 4 = 50%

        // Deterministic Rule
        var ruleItem = result.TopViolatedGuidelines.FirstOrDefault(g => g.IsDeterministicRule);
        ruleItem.Should().NotBeNull();
        ruleItem!.GuidelineOrRuleName.Should().Contain("Guaranteed Returns Prohibition");
        ruleItem.ViolationCount.Should().Be(1);
        ruleItem.HighRiskCount.Should().Be(1);
        ruleItem.PercentageOfTotal.Should().Be(25.0);

        // AI Heuristic
        var heuristicItem = result.TopViolatedGuidelines.FirstOrDefault(g => !g.IsVerifiedGuideline && !g.IsDeterministicRule);
        heuristicItem.Should().NotBeNull();
        heuristicItem!.GuidelineOrRuleName.Should().Contain("Heuristic AI Finding");
        heuristicItem.ViolationCount.Should().Be(1);
        heuristicItem.LowRiskCount.Should().Be(1);
        heuristicItem.PercentageOfTotal.Should().Be(25.0);
    }

    [Fact]
    public async Task GovernanceHealthScore_BoundariesAndClamping_ShouldStayBetween0And100()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var sub = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "High Violation Batch",
            ContentText = "Content",
            Status = ContentSubmissionStatus.Rejected,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.ContentSubmissions.Add(sub);

        var run = new ValidationRun { Id = Guid.NewGuid(), ContentSubmissionId = sub.Id, StartedAt = DateTime.UtcNow.AddDays(-1) };
        context.ValidationRuns.Add(run);

        // Add 30 High risk findings to test score reduction and floor clamping at 0
        for (int i = 0; i < 30; i++)
        {
            context.ValidationFindings.Add(new ValidationFinding
            {
                Id = Guid.NewGuid(),
                ValidationRunId = run.Id,
                Severity = RiskSeverity.High,
                Explanation = $"Severe Violation {i}",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
        }

        context.ReviewDecisions.Add(new ReviewDecision
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = sub.Id,
            ReviewerName = "Lead Auditor",
            Decision = ReviewDecisionType.Rejected,
            ReviewedAt = DateTime.UtcNow.AddDays(-1)
        });

        await context.SaveChangesAsync();

        var service = new DashboardService(context);

        // Act
        var result = await service.GetGovernanceIntelligenceAsync(30);

        // Assert
        result.HealthScore.Score.Should().BeInRange(0.0, 100.0);
        result.HealthScore.Score.Should().Be(0.0); // Floored at 0
        result.HealthScore.StatusLabel.Should().Contain("Critical");
    }

    [Fact]
    public async Task DateRangeFiltering_ShouldFilterDataByRequestedDays()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        // Sub 1: 5 days ago (in 7 days window)
        var subRecent = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Recent Sub",
            ContentText = "Recent",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        // Sub 2: 20 days ago (in 30 days window, but not 7 days)
        var subOlder = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Older Sub",
            ContentText = "Older",
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        context.ContentSubmissions.AddRange(subRecent, subOlder);

        var runRecent = new ValidationRun { Id = Guid.NewGuid(), ContentSubmissionId = subRecent.Id, StartedAt = DateTime.UtcNow.AddDays(-5) };
        var runOlder = new ValidationRun { Id = Guid.NewGuid(), ContentSubmissionId = subOlder.Id, StartedAt = DateTime.UtcNow.AddDays(-20) };
        context.ValidationRuns.AddRange(runRecent, runOlder);

        context.ValidationFindings.Add(new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = runRecent.Id,
            Severity = RiskSeverity.High,
            Explanation = "Recent high",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });
        context.ValidationFindings.Add(new ValidationFinding
        {
            Id = Guid.NewGuid(),
            ValidationRunId = runOlder.Id,
            Severity = RiskSeverity.Medium,
            Explanation = "Older med",
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        });

        await context.SaveChangesAsync();

        var service = new DashboardService(context);

        // Act: Request 7 days
        var result7 = await service.GetGovernanceIntelligenceAsync(7);
        // Act: Request 30 days
        var result30 = await service.GetGovernanceIntelligenceAsync(30);

        // Assert: 7-day query
        result7.Overview.TotalSubmissions.Should().Be(1);
        result7.Overview.TotalFindings.Should().Be(1);
        result7.Overview.HighRiskFindings.Should().Be(1);
        result7.Overview.MediumRiskFindings.Should().Be(0);

        // Assert: 30-day query
        result30.Overview.TotalSubmissions.Should().Be(2);
        result30.Overview.TotalFindings.Should().Be(2);
        result30.Overview.HighRiskFindings.Should().Be(1);
        result30.Overview.MediumRiskFindings.Should().Be(1);
    }
}
