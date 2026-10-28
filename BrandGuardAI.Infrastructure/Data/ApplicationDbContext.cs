using Microsoft.EntityFrameworkCore;
using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BrandGuideline> BrandGuidelines => Set<BrandGuideline>();
    public DbSet<GuidelineChunk> GuidelineChunks => Set<GuidelineChunk>();
    public DbSet<ContentSubmission> ContentSubmissions => Set<ContentSubmission>();
    public DbSet<ValidationRun> ValidationRuns => Set<ValidationRun>();
    public DbSet<ValidationFinding> ValidationFindings => Set<ValidationFinding>();
    public DbSet<ReviewDecision> ReviewDecisions => Set<ReviewDecision>();
    public DbSet<ValidationRule> ValidationRules => Set<ValidationRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BrandGuideline
        modelBuilder.Entity<BrandGuideline>(entity =>
        {
            entity.ToTable("BrandGuidelines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(250);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(250);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.HasMany(e => e.Chunks)
                  .WithOne(c => c.BrandGuideline)
                  .HasForeignKey(c => c.BrandGuidelineId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ValidationRules)
                  .WithOne(r => r.SourceGuideline)
                  .HasForeignKey(r => r.SourceGuidelineId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // GuidelineChunk
        modelBuilder.Entity<GuidelineChunk>(entity =>
        {
            entity.ToTable("GuidelineChunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChunkText).IsRequired();
            entity.Property(e => e.SectionTitle).HasMaxLength(250);
            entity.HasIndex(e => e.BrandGuidelineId);
            entity.HasIndex(e => new { e.BrandGuidelineId, e.ChunkIndex });
        });

        // ContentSubmission
        modelBuilder.Entity<ContentSubmission>(entity =>
        {
            entity.ToTable("ContentSubmissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(250);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContentText).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasMany(e => e.ValidationRuns)
                  .WithOne(v => v.ContentSubmission)
                  .HasForeignKey(v => v.ContentSubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ReviewDecisions)
                  .WithOne(r => r.ContentSubmission)
                  .HasForeignKey(r => r.ContentSubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ValidationRun
        modelBuilder.Entity<ValidationRun>(entity =>
        {
            entity.ToTable("ValidationRuns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.OverallRiskLevel).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(e => e.ContentSubmissionId);
            entity.HasMany(e => e.Findings)
                  .WithOne(f => f.ValidationRun)
                  .HasForeignKey(f => f.ValidationRunId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ReviewDecisions)
                  .WithOne(r => r.ValidationRun)
                  .HasForeignKey(r => r.ValidationRunId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ValidationFinding
        modelBuilder.Entity<ValidationFinding>(entity =>
        {
            entity.ToTable("ValidationFindings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IssueType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.OriginalText).IsRequired();
            entity.Property(e => e.Explanation).IsRequired();
            entity.Property(e => e.SuggestedFix).IsRequired();
            entity.HasIndex(e => e.ValidationRunId);
            entity.HasIndex(e => e.GuidelineChunkId);
            entity.HasOne(e => e.GuidelineChunk)
                  .WithMany(c => c.Findings)
                  .HasForeignKey(e => e.GuidelineChunkId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ReviewDecision
        modelBuilder.Entity<ReviewDecision>(entity =>
        {
            entity.ToTable("ReviewDecisions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReviewerName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Decision).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Comments).HasMaxLength(2000);
            entity.HasIndex(e => e.ContentSubmissionId);
            entity.HasIndex(e => e.ReviewedAt);
        });

        // ValidationRule
        modelBuilder.Entity<ValidationRule>(entity =>
        {
            entity.ToTable("ValidationRules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RuleType).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.RuleValue).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => new { e.RuleType, e.IsActive });
        });

        // Seed Core Deterministic Governance Rules
        SeedDefaultRules(modelBuilder);
    }

    private static void SeedDefaultRules(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ValidationRule>().HasData(
            new ValidationRule
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                RuleName = "No Absolute Guarantees",
                RuleType = RuleType.AbsoluteGuarantee,
                RuleValue = @"\b(guarantees?|guaranteed|100%\s+results?|100%\s+success|never\s+fails?|zero\s+risk|risk[\s-]free)\b",
                Severity = RiskSeverity.High,
                Description = "Marketing content must not contain absolute outcome guarantees or promise 100% certainty.",
                IsActive = true
            },
            new ValidationRule
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                RuleName = "Superlative Claims Verification",
                RuleType = RuleType.RestrictedPhrase,
                RuleValue = @"\b(best(\s+[a-zA-Z]+)?\s+in\s+the\s+world|#1(\s+[a-zA-Z]+)?\s+in\s+the\s+world|unmatched|superior\s+to\s+all|unbeatable)\b",
                Severity = RiskSeverity.High,
                Description = "Unsubstantiated claims claiming #1 ranking or world-best status require documented proof and accreditation.",
                IsActive = true
            },
            new ValidationRule
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                RuleName = "Prohibited Medical Claims",
                RuleType = RuleType.ProhibitedTerm,
                RuleValue = @"\b(cures?|eliminates\s+disease|miracle\s+cure|fda[\s-]certified|clinically\s+proven\s+cure)\b",
                Severity = RiskSeverity.High,
                Description = "Marketing copy must never claim to cure illnesses or misrepresent medical clinical clearance.",
                IsActive = true
            },
            new ValidationRule
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                RuleName = "Financial Guarantee Prohibited",
                RuleType = RuleType.ProhibitedTerm,
                RuleValue = @"\b(get\s+rich|double\s+your\s+money|guaranteed\s+returns?|fail[\s-]safe\s+profit)\b",
                Severity = RiskSeverity.High,
                Description = "Unapproved financial promises and return guarantees are strictly prohibited.",
                IsActive = true
            },
            new ValidationRule
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                RuleName = "Required Promotional Disclaimer",
                RuleType = RuleType.RequiredDisclaimer,
                RuleValue = @"terms\s+and\s+conditions\s+apply|results\s+may\s+vary|individual\s+results\s+may\s+differ",
                Severity = RiskSeverity.Medium,
                Description = "Promotional offers and outcome statements must include standard disclaimer language.",
                IsActive = true
            }
        );
    }
}
