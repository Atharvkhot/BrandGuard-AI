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

public class ReviewServiceTests
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
    public async Task SubmitDecisionAsync_ShouldPersistDecisionAndUpdateSubmissionStatus()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var submission = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Fall Campaign Copy",
            ContentText = "Exciting autumn discounts across all categories.",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);

        var dto = new SubmitReviewDecisionDto(
            submission.Id,
            null,
            "Atharv Khot",
            ReviewDecisionType.ChangesRequested,
            "Please remove absolute phrasing and re-run validation."
        );

        // Act
        var result = await service.SubmitDecisionAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be(ReviewDecisionType.ChangesRequested);
        result.ReviewerName.Should().Be("Atharv Khot");

        var updatedSubmission = await context.ContentSubmissions.FindAsync(submission.Id);
        updatedSubmission!.Status.Should().Be(ContentSubmissionStatus.ChangesRequested);

        var history = await service.GetReviewHistoryAsync();
        history.Should().HaveCount(1);
        history[0].ReviewerName.Should().Be("Atharv Khot");
    }

    [Fact]
    public async Task SubmitDecisionAsync_ShouldPreventDuplicateDecisions()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var submission = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Product Brochure",
            ContentText = "Product Brochure text",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var dto = new SubmitReviewDecisionDto(
            submission.Id,
            null,
            "Ishwari Kadam",
            ReviewDecisionType.Approved,
            "Approved for immediate publication."
        );

        // Act - Submit twice in quick succession
        var result1 = await service.SubmitDecisionAsync(dto);
        var result2 = await service.SubmitDecisionAsync(dto);

        // Assert
        result1.Id.Should().Be(result2.Id); // Should return existing decision without creating duplicate
        var count = await context.ReviewDecisions.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task SubmitDecisionAsync_WhenSubmissionNotFound_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var nonExistentId = Guid.NewGuid();
        var dto = new SubmitReviewDecisionDto(
            nonExistentId,
            null,
            "QA Auditor",
            ReviewDecisionType.Approved,
            "Approval"
        );

        // Act
        var act = async () => await service.SubmitDecisionAsync(dto);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SubmitDecisionAsync_WithBlankReviewerName_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var submission = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Draft",
            ContentText = "Draft text",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var dto = new SubmitReviewDecisionDto(
            submission.Id,
            null,
            "   ",
            ReviewDecisionType.Approved,
            "Comments"
        );

        // Act
        var act = async () => await service.SubmitDecisionAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SubmitDecisionAsync_RejectDecision_ShouldUpdateStatusToRejected()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var submission = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = "Unapproved Medical Claim",
            ContentText = "Content text",
            Status = ContentSubmissionStatus.PendingReview
        };
        context.ContentSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var service = new ReviewService(context, NullLogger<ReviewService>.Instance);
        var dto = new SubmitReviewDecisionDto(
            submission.Id,
            null,
            "Legal Compliance Director",
            ReviewDecisionType.Rejected,
            "Policy violation: clinical promises are prohibited."
        );

        // Act
        var result = await service.SubmitDecisionAsync(dto);

        // Assert
        result.Decision.Should().Be(ReviewDecisionType.Rejected);
        var updated = await context.ContentSubmissions.FindAsync(submission.Id);
        updated!.Status.Should().Be(ContentSubmissionStatus.Rejected);
    }
}
