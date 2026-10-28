using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Services;

public class ReviewService : IReviewService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        IApplicationDbContext context,
        ILogger<ReviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ReviewDecisionDto> SubmitDecisionAsync(SubmitReviewDecisionDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ReviewerName))
        {
            throw new ArgumentException("Reviewer name is required.", nameof(dto.ReviewerName));
        }

        var submission = await _context.ContentSubmissions
            .Include(s => s.ValidationRuns)
            .Include(s => s.ReviewDecisions)
            .FirstOrDefaultAsync(s => s.Id == dto.ContentSubmissionId, cancellationToken);

        if (submission == null)
        {
            throw new KeyNotFoundException($"Content submission {dto.ContentSubmissionId} not found.");
        }

        // Prevent duplicate immediate decisions within 30 seconds with identical status
        var existingRecentDecision = submission.ReviewDecisions
            .OrderByDescending(d => d.ReviewedAt)
            .FirstOrDefault();

        if (existingRecentDecision != null &&
            existingRecentDecision.Decision == dto.Decision &&
            existingRecentDecision.ReviewerName.Equals(dto.ReviewerName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            DateTime.UtcNow.Subtract(existingRecentDecision.ReviewedAt).TotalSeconds < 15)
        {
            _logger.LogWarning("Duplicate review decision detected for submission {SubmissionId}. Returning existing decision.", dto.ContentSubmissionId);
            return new ReviewDecisionDto(
                existingRecentDecision.Id,
                submission.Id,
                existingRecentDecision.ValidationRunId,
                submission.Title,
                submission.ContentType,
                existingRecentDecision.ReviewerName,
                existingRecentDecision.Decision,
                existingRecentDecision.Comments,
                existingRecentDecision.ReviewedAt
            );
        }

        var runId = dto.ValidationRunId ?? submission.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => (Guid?)v.Id).FirstOrDefault();

        var decision = new ReviewDecision
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = submission.Id,
            ValidationRunId = runId,
            ReviewerName = dto.ReviewerName.Trim(),
            Decision = dto.Decision,
            Comments = dto.Comments?.Trim() ?? string.Empty,
            ReviewedAt = DateTime.UtcNow
        };

        // Update submission status based on human decision
        submission.Status = dto.Decision switch
        {
            ReviewDecisionType.Approved => ContentSubmissionStatus.Approved,
            ReviewDecisionType.ChangesRequested => ContentSubmissionStatus.ChangesRequested,
            ReviewDecisionType.Rejected => ContentSubmissionStatus.Rejected,
            _ => ContentSubmissionStatus.PendingReview
        };
        submission.UpdatedAt = DateTime.UtcNow;

        _context.ReviewDecisions.Add(decision);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Human Review Decision recorded: {Decision} by {Reviewer} for submission '{Title}'",
            decision.Decision, decision.ReviewerName, submission.Title);

        return new ReviewDecisionDto(
            decision.Id,
            submission.Id,
            decision.ValidationRunId,
            submission.Title,
            submission.ContentType,
            decision.ReviewerName,
            decision.Decision,
            decision.Comments,
            decision.ReviewedAt
        );
    }

    public async Task<List<ReviewHistoryItemDto>> GetReviewHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ReviewDecisions
            .Include(d => d.ContentSubmission)
            .Include(d => d.ValidationRun)
            .OrderByDescending(d => d.ReviewedAt)
            .Select(d => new ReviewHistoryItemDto(
                d.Id,
                d.ContentSubmissionId,
                d.ValidationRunId,
                d.ContentSubmission != null ? d.ContentSubmission.Title : "Unknown Content",
                d.ContentSubmission != null ? d.ContentSubmission.ContentType : "General",
                d.ReviewerName,
                d.Decision,
                d.Comments,
                d.ReviewedAt,
                d.ValidationRun != null ? (RiskSeverity?)d.ValidationRun.OverallRiskLevel : null,
                d.ValidationRun != null ? d.ValidationRun.TotalFindings : 0
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReviewDecisionDto?> GetDecisionBySubmissionIdAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var decision = await _context.ReviewDecisions
            .Include(d => d.ContentSubmission)
            .Where(d => d.ContentSubmissionId == submissionId)
            .OrderByDescending(d => d.ReviewedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (decision == null)
        {
            return null;
        }

        return new ReviewDecisionDto(
            decision.Id,
            decision.ContentSubmissionId,
            decision.ValidationRunId,
            decision.ContentSubmission?.Title ?? "Unknown Content",
            decision.ContentSubmission?.ContentType ?? "General",
            decision.ReviewerName,
            decision.Decision,
            decision.Comments,
            decision.ReviewedAt
        );
    }
}
