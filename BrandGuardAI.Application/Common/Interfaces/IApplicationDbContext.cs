using Microsoft.EntityFrameworkCore;
using BrandGuardAI.Domain.Entities;

namespace BrandGuardAI.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<BrandGuideline> BrandGuidelines { get; }
    DbSet<GuidelineChunk> GuidelineChunks { get; }
    DbSet<ContentSubmission> ContentSubmissions { get; }
    DbSet<ValidationRun> ValidationRuns { get; }
    DbSet<ValidationFinding> ValidationFindings { get; }
    DbSet<ReviewDecision> ReviewDecisions { get; }
    DbSet<ValidationRule> ValidationRules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
