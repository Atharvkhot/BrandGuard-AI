using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Services;

public class ContentSubmissionService : IContentSubmissionService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ContentSubmissionService> _logger;

    public ContentSubmissionService(
        IApplicationDbContext context,
        ILogger<ContentSubmissionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ContentSubmissionDto> CreateSubmissionAsync(CreateContentSubmissionDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new ArgumentException("Content title is required.", nameof(dto.Title));
        }
        if (string.IsNullOrWhiteSpace(dto.ContentText))
        {
            throw new ArgumentException("Marketing content cannot be empty.", nameof(dto.ContentText));
        }

        var submission = new ContentSubmission
        {
            Id = Guid.NewGuid(),
            Title = dto.Title.Trim(),
            ContentType = string.IsNullOrWhiteSpace(dto.ContentType) ? "Social Media Post" : dto.ContentType,
            ContentText = dto.ContentText.Trim(),
            Status = ContentSubmissionStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContentSubmissions.Add(submission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new content submission '{Title}' (ID: {Id})", submission.Title, submission.Id);

        return new ContentSubmissionDto(
            submission.Id,
            submission.Title,
            submission.ContentType,
            submission.ContentText,
            submission.Status,
            submission.CreatedAt,
            submission.UpdatedAt,
            null,
            null,
            0
        );
    }

    public async Task<List<ContentSubmissionDto>> GetAllSubmissionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ContentSubmissions
            .Include(s => s.ValidationRuns)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ContentSubmissionDto(
                s.Id,
                s.Title,
                s.ContentType,
                s.ContentText,
                s.Status,
                s.CreatedAt,
                s.UpdatedAt,
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => (Guid?)v.Id).FirstOrDefault(),
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => (RiskSeverity?)v.OverallRiskLevel).FirstOrDefault(),
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => v.TotalFindings).FirstOrDefault()
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ContentSubmissionDetailDto?> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var submission = await _context.ContentSubmissions
            .Include(s => s.ValidationRuns)
            .Include(s => s.ReviewDecisions)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (submission == null)
        {
            return null;
        }

        var latestRun = submission.ValidationRuns.OrderByDescending(v => v.StartedAt).FirstOrDefault();

        var decisions = submission.ReviewDecisions
            .OrderByDescending(d => d.ReviewedAt)
            .Select(d => new ReviewDecisionSummaryDto(
                d.Id,
                d.ReviewerName,
                d.Decision,
                d.Comments,
                d.ReviewedAt
            ))
            .ToList();

        return new ContentSubmissionDetailDto(
            submission.Id,
            submission.Title,
            submission.ContentType,
            submission.ContentText,
            submission.Status,
            submission.CreatedAt,
            submission.UpdatedAt,
            latestRun?.Id,
            latestRun?.OverallRiskLevel,
            latestRun?.TotalFindings ?? 0,
            decisions
        );
    }

    public async Task<ContentSubmissionDto> UpdateSubmissionAsync(Guid id, UpdateContentSubmissionDto dto, CancellationToken cancellationToken = default)
    {
        var submission = await _context.ContentSubmissions
            .Include(s => s.ValidationRuns)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (submission == null)
        {
            throw new KeyNotFoundException($"Content submission {id} not found.");
        }

        submission.Title = dto.Title.Trim();
        submission.ContentType = dto.ContentType;
        submission.ContentText = dto.ContentText.Trim();
        submission.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var latestRun = submission.ValidationRuns.OrderByDescending(v => v.StartedAt).FirstOrDefault();

        return new ContentSubmissionDto(
            submission.Id,
            submission.Title,
            submission.ContentType,
            submission.ContentText,
            submission.Status,
            submission.CreatedAt,
            submission.UpdatedAt,
            latestRun?.Id,
            latestRun?.OverallRiskLevel,
            latestRun?.TotalFindings ?? 0
        );
    }

    public async Task<ValidationResultDto?> GetLatestValidationResultAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var run = await _context.ValidationRuns
            .Include(v => v.ContentSubmission)
            .Include(v => v.Findings)
                .ThenInclude(f => f.GuidelineChunk)
                    .ThenInclude(c => c!.BrandGuideline)
            .Where(v => v.ContentSubmissionId == submissionId)
            .OrderByDescending(v => v.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (run == null)
        {
            return null;
        }

        var highCount = run.Findings.Count(f => f.Severity == RiskSeverity.High);
        var medCount = run.Findings.Count(f => f.Severity == RiskSeverity.Medium);
        var lowCount = run.Findings.Count(f => f.Severity == RiskSeverity.Low);

        var findings = run.Findings.Select(f => new FindingDto(
            f.Id,
            f.ValidationRunId,
            f.GuidelineChunkId,
            f.IssueType,
            f.Severity,
            f.OriginalText,
            f.Explanation,
            f.SuggestedFix,
            f.EvidenceText,
            f.GuidelineChunk?.BrandGuideline?.Title ?? "Brand Guideline",
            f.GuidelineChunk?.PageNumber,
            f.GuidelineChunk?.SectionTitle,
            f.CreatedAt
        )).ToList();

        return new ValidationResultDto(
            run.Id,
            run.ContentSubmissionId,
            run.ContentSubmission?.Title ?? "Untitled Content",
            run.ContentSubmission?.ContentType ?? "General",
            run.ContentSubmission?.ContentText ?? string.Empty,
            run.Status,
            run.OverallRiskLevel,
            run.TotalFindings,
            highCount,
            medCount,
            lowCount,
            run.StartedAt,
            run.CompletedAt,
            run.ErrorMessage,
            findings
        );
    }
}
