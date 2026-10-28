using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BrandGuardAI.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;

    public DashboardService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalGuidelines = await _context.BrandGuidelines.CountAsync(cancellationToken);
        var totalSubmissions = await _context.ContentSubmissions.CountAsync(cancellationToken);

        var pendingReviews = await _context.ContentSubmissions
            .CountAsync(s => s.Status == ContentSubmissionStatus.PendingReview || s.Status == ContentSubmissionStatus.Validating, cancellationToken);

        var approvedSubmissions = await _context.ContentSubmissions
            .CountAsync(s => s.Status == ContentSubmissionStatus.Approved, cancellationToken);

        var rejectedSubmissions = await _context.ContentSubmissions
            .CountAsync(s => s.Status == ContentSubmissionStatus.Rejected, cancellationToken);

        var changesRequested = await _context.ContentSubmissions
            .CountAsync(s => s.Status == ContentSubmissionStatus.ChangesRequested, cancellationToken);

        var highRiskFindings = await _context.ValidationFindings
            .CountAsync(f => f.Severity == RiskSeverity.High, cancellationToken);

        var mediumRiskFindings = await _context.ValidationFindings
            .CountAsync(f => f.Severity == RiskSeverity.Medium, cancellationToken);

        var lowRiskFindings = await _context.ValidationFindings
            .CountAsync(f => f.Severity == RiskSeverity.Low, cancellationToken);

        var recentSubmissions = await _context.ContentSubmissions
            .Include(s => s.ValidationRuns)
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .Select(s => new RecentSubmissionItemDto(
                s.Id,
                s.Title,
                s.ContentType,
                s.CreatedAt,
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => (RiskSeverity?)v.OverallRiskLevel).FirstOrDefault(),
                s.Status,
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => v.TotalFindings).FirstOrDefault(),
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => (Guid?)v.Id).FirstOrDefault()
            ))
            .ToListAsync(cancellationToken);

        return new DashboardStatsDto(
            totalGuidelines,
            totalSubmissions,
            pendingReviews,
            approvedSubmissions,
            rejectedSubmissions,
            changesRequested,
            highRiskFindings,
            mediumRiskFindings,
            lowRiskFindings,
            recentSubmissions
        );
    }
}
