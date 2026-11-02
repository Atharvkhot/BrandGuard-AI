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

    public async Task<AcknowledgeFindingsResultDto> AcknowledgeFindingsAsync(AcknowledgeFindingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || request.FindingIds == null || request.FindingIds.Count == 0)
        {
            throw new ArgumentException("At least one finding ID must be provided.", nameof(request.FindingIds));
        }

        var distinctFindingIds = request.FindingIds.Distinct().ToList();
        var findings = await _context.ValidationFindings
            .Where(f => distinctFindingIds.Contains(f.Id))
            .ToListAsync(cancellationToken);

        if (findings.Count == 0)
        {
            return new AcknowledgeFindingsResultDto(0, new List<Guid>());
        }

        var reviewer = string.IsNullOrWhiteSpace(request.AcknowledgedBy) ? "Reviewer" : request.AcknowledgedBy.Trim();
        var now = DateTime.UtcNow;

        foreach (var finding in findings)
        {
            finding.IsAcknowledged = true;
            finding.AcknowledgedAt = now;
            finding.AcknowledgedBy = reviewer;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Batch acknowledged {Count} findings by reviewer '{Reviewer}'",
            findings.Count, reviewer);

        return new AcknowledgeFindingsResultDto(
            findings.Count,
            findings.Select(f => f.Id).ToList()
        );
    }

    public async Task<BatchReviewResultDto> SubmitBatchDecisionsAsync(BatchReviewDecisionDto request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ReviewerName))
        {
            throw new ArgumentException("Reviewer name is required for governance auditability.", nameof(request.ReviewerName));
        }

        if (request.ContentSubmissionIds == null || request.ContentSubmissionIds.Count == 0)
        {
            throw new ArgumentException("At least one submission ID must be provided.", nameof(request.ContentSubmissionIds));
        }

        if (!Enum.IsDefined(typeof(ReviewDecisionType), request.Decision))
        {
            throw new ArgumentException($"Invalid review decision value: {request.Decision}", nameof(request.Decision));
        }

        var distinctIds = request.ContentSubmissionIds.Distinct().ToList();
        var submissions = await _context.ContentSubmissions
            .Include(s => s.ValidationRuns)
            .Include(s => s.ReviewDecisions)
            .Where(s => distinctIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var submissionMap = submissions.ToDictionary(s => s.Id);
        var results = new List<BatchReviewItemResultDto>();
        var reviewer = request.ReviewerName.Trim();
        var comments = request.Comments?.Trim() ?? string.Empty;
        var now = DateTime.UtcNow;

        foreach (var id in distinctIds)
        {
            if (!submissionMap.TryGetValue(id, out var submission))
            {
                results.Add(new BatchReviewItemResultDto(id, "Unknown", false, $"Submission {id} not found.", null));
                continue;
            }

            var latestRun = submission.ValidationRuns.OrderByDescending(r => r.StartedAt).FirstOrDefault();

            // Safety guard: Prevent accidental approval of high-risk content
            if (request.Decision == ReviewDecisionType.Approved &&
                latestRun != null &&
                latestRun.OverallRiskLevel == RiskSeverity.High &&
                !request.ConfirmHighRiskApproval)
            {
                _logger.LogWarning("Blocked automatic approval for high-risk submission {SubmissionId} ('{Title}') - explicit high-risk confirmation override was not provided.",
                    submission.Id, submission.Title);

                results.Add(new BatchReviewItemResultDto(
                    submission.Id,
                    submission.Title,
                    false,
                    "Approval blocked: submission contains High Risk findings. Explicit confirmation override is required.",
                    null
                ));
                continue;
            }

            // Duplicate safety: Check if identical decision was recorded in the last 15 seconds
            var recentDecision = submission.ReviewDecisions.OrderByDescending(d => d.ReviewedAt).FirstOrDefault();
            if (recentDecision != null &&
                recentDecision.Decision == request.Decision &&
                recentDecision.ReviewerName.Equals(reviewer, StringComparison.OrdinalIgnoreCase) &&
                now.Subtract(recentDecision.ReviewedAt).TotalSeconds < 15)
            {
                _logger.LogInformation("Duplicate review decision detected for submission {SubmissionId} in batch. Preserving existing record.", submission.Id);
                results.Add(new BatchReviewItemResultDto(submission.Id, submission.Title, true, null, request.Decision));
                continue;
            }

            var decision = new ReviewDecision
            {
                Id = Guid.NewGuid(),
                ContentSubmissionId = submission.Id,
                ValidationRunId = latestRun?.Id,
                ReviewerName = reviewer,
                Decision = request.Decision,
                Comments = comments,
                ReviewedAt = now
            };

            submission.Status = request.Decision switch
            {
                ReviewDecisionType.Approved => ContentSubmissionStatus.Approved,
                ReviewDecisionType.ChangesRequested => ContentSubmissionStatus.ChangesRequested,
                ReviewDecisionType.Rejected => ContentSubmissionStatus.Rejected,
                _ => ContentSubmissionStatus.PendingReview
            };
            submission.UpdatedAt = now;

            _context.ReviewDecisions.Add(decision);
            results.Add(new BatchReviewItemResultDto(submission.Id, submission.Title, true, null, request.Decision));
        }

        // Persist all valid changes in an atomic database operation
        await _context.SaveChangesAsync(cancellationToken);

        int succeeded = results.Count(r => r.Success);
        int failed = results.Count(r => !r.Success);

        _logger.LogInformation("Batch review completed by '{Reviewer}': {Succeeded} succeeded, {Failed} failed out of {Total} submissions.",
            reviewer, succeeded, failed, results.Count);

        return new BatchReviewResultDto(results.Count, succeeded, failed, results);
    }
}

