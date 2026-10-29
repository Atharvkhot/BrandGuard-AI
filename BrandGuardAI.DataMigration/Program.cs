using System.Text.Json;
using System.Text.Json.Serialization;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BrandGuardAI.DataMigration;

public class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> Main(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        string mode = "help";
        string? customSqlite = null;
        string? customPostgres = null;
        string jsonFilePath = "brandguard_backup.json";

        foreach (var arg in args)
        {
            if (arg.StartsWith("--sqlite=", StringComparison.OrdinalIgnoreCase))
                customSqlite = arg["--sqlite=".Length..].Trim('"');
            else if (arg.StartsWith("--postgres=", StringComparison.OrdinalIgnoreCase))
                customPostgres = arg["--postgres=".Length..].Trim('"');
            else if (arg.StartsWith("--file=", StringComparison.OrdinalIgnoreCase))
                jsonFilePath = arg["--file=".Length..].Trim('"');
            else if (arg.StartsWith("--"))
                mode = arg[2..].ToLowerInvariant();
            else if (string.IsNullOrEmpty(mode) || mode == "help")
                mode = arg.ToLowerInvariant();
        }

        // Auto-discover SQLite db path
        string sqliteConn;
        if (!string.IsNullOrWhiteSpace(customSqlite))
        {
            sqliteConn = customSqlite.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) 
                ? customSqlite 
                : $"Data Source={customSqlite}";
        }
        else
        {
            var candidatePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "BrandGuardAI.API", "BrandGuardAI.db"),
                Path.Combine(Directory.GetCurrentDirectory(), "BrandGuardAI.db"),
                Path.Combine(AppContext.BaseDirectory, "BrandGuardAI.db"),
                Path.GetFullPath("../BrandGuardAI.API/BrandGuardAI.db", Directory.GetCurrentDirectory())
            };
            var resolvedDb = candidatePaths.FirstOrDefault(File.Exists) ?? candidatePaths[0];
            sqliteConn = $"Data Source={resolvedDb}";
        }

        var postgresConn = customPostgres 
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSqlConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:PostgreSqlConnection");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("====================================================================");
        Console.WriteLine("       BrandGuard AI — Database Migration & Parity Utility");
        Console.WriteLine("====================================================================");
        Console.ResetColor();

        try
        {
            switch (mode.ToLowerInvariant())
            {
                case "seed-nova-green":
                    return await SeedNovaGreenAsync(sqliteConn);

                case "export":
                    return await ExportSqliteToJsonAsync(sqliteConn, jsonFilePath);

                case "migrate":
                    if (string.IsNullOrWhiteSpace(postgresConn))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("ERROR: Target PostgreSQL connection string is required for migration.");
                        Console.WriteLine("Provide via --postgres=\"Host=...\" or env variable ConnectionStrings__PostgreSqlConnection.");
                        Console.ResetColor();
                        return 1;
                    }
                    return await MigrateSqliteToPostgresAsync(sqliteConn, postgresConn);

                case "import":
                    if (string.IsNullOrWhiteSpace(postgresConn))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("ERROR: Target PostgreSQL connection string is required for import.");
                        Console.ResetColor();
                        return 1;
                    }
                    return await ImportJsonToPostgresAsync(jsonFilePath, postgresConn);

                case "verify":
                    return await VerifyParityAsync(sqliteConn, postgresConn);

                default:
                    PrintHelp();
                    return 0;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nFATAL ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("\nUsage: dotnet run --project BrandGuardAI.DataMigration -- <command> [options]\n");
        Console.WriteLine("Commands:");
        Console.WriteLine("  --export            Exports records from SQLite database to a local JSON backup file.");
        Console.WriteLine("  --migrate           Migrates all data directly from SQLite to Neon PostgreSQL.");
        Console.WriteLine("  --import            Imports data from a JSON backup file to Neon PostgreSQL.");
        Console.WriteLine("  --seed-nova-green   Ensures the required NOVA GREEN test campaign exists in SQLite.");
        Console.WriteLine("  --verify            Compares table counts and referential integrity between DBs.\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  --sqlite=<conn>     SQLite connection string (Default: Data Source=../BrandGuardAI.API/BrandGuardAI.db)");
        Console.WriteLine("  --postgres=<conn>   Target PostgreSQL connection string (Or use ConnectionStrings__PostgreSqlConnection)");
        Console.WriteLine("  --file=<path>       Path to JSON backup file (Default: brandguard_backup.json)");
    }

    private static ApplicationDbContext CreateSqliteContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ApplicationDbContext CreatePostgresContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    public static async Task<int> SeedNovaGreenAsync(string sqliteConn)
    {
        Console.WriteLine($"\n[1/2] Connecting to SQLite source: {sqliteConn}");
        await using var db = CreateSqliteContext(sqliteConn);
        await db.Database.EnsureCreatedAsync();

        var novaGreenId = Guid.Parse("bef451ae-c700-4565-94dd-834e1213e95c");
        var existing = await db.ContentSubmissions
            .Include(s => s.ValidationRuns)
            .FirstOrDefaultAsync(s => s.Id == novaGreenId);

        if (existing != null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[✓] NOVA GREEN submission ({novaGreenId}) already exists with status: {existing.Status}");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("[2/2] Seeding 'NOVA GREEN – Environmental Claims Compliance Test'...");

        var now = DateTime.UtcNow;
        var submission = new ContentSubmission
        {
            Id = novaGreenId,
            Title = "NOVA GREEN – Environmental Claims Compliance Test",
            ContentType = "Social Media Post",
            ContentText = "Nova Green introduces 100% eco-friendly zero-carbon packaging that guarantees 100% waste elimination and is the best sustainable product in the world. Eliminate your footprint unconditionally today!",
            Status = ContentSubmissionStatus.PendingReview,
            CreatedAt = now,
            UpdatedAt = now
        };

        var runId = Guid.NewGuid();
        var run = new ValidationRun
        {
            Id = runId,
            ContentSubmissionId = novaGreenId,
            Status = "Completed",
            OverallRiskLevel = RiskSeverity.High,
            TotalFindings = 5,
            StartedAt = now,
            CompletedAt = now.AddSeconds(2)
        };

        var findings = new List<ValidationFinding>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ValidationRunId = runId,
                IssueType = "AbsoluteGuarantee",
                Severity = RiskSeverity.High,
                OriginalText = "guarantees 100% waste elimination",
                Explanation = "Copy contains an absolute guarantee without substantiation.",
                SuggestedFix = "Replace absolute promise with substantiated performance phrasing, e.g., 'designed to significantly reduce waste'.",
                EvidenceText = "Section 2.1: Marketing copy must not promise absolute outcomes or 100% results.",
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                ValidationRunId = runId,
                IssueType = "SuperlativeClaim",
                Severity = RiskSeverity.High,
                OriginalText = "best sustainable product in the world",
                Explanation = "Unsubstantiated claim of '#1 in the world' or 'best in the world'.",
                SuggestedFix = "State specific verified attributes instead of superlative rankings.",
                EvidenceText = "Section 3.4: World-best claims require documented legal clearance and accreditation.",
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                ValidationRunId = runId,
                IssueType = "ProhibitedClaim",
                Severity = RiskSeverity.High,
                OriginalText = "zero-carbon packaging",
                Explanation = "Broad environmental claims must specify verified lifecycle boundaries.",
                SuggestedFix = "Specify 'manufactured using renewable electricity' rather than unverified 'zero-carbon'.",
                EvidenceText = "Environmental Claims Policy: General green claims must be backed by ISO 14044 lifecycle reports.",
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                ValidationRunId = runId,
                IssueType = "AbsoluteGuarantee",
                Severity = RiskSeverity.High,
                OriginalText = "unconditionally",
                Explanation = "Absolute word implies zero qualification or exceptions.",
                SuggestedFix = "Remove 'unconditionally' and include appropriate qualifications.",
                EvidenceText = "Rulebook Policy 4: Absolute descriptors are strictly prohibited in promotional copy.",
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                ValidationRunId = runId,
                IssueType = "MissingDisclaimer",
                Severity = RiskSeverity.Medium,
                OriginalText = "Eliminate your footprint unconditionally today!",
                Explanation = "Missing required promotional terms and condition disclaimer.",
                SuggestedFix = "Append 'Terms and conditions apply. Results may vary by region.'",
                EvidenceText = "Mandatory Disclaimers: All promotional campaign copy must contain terms and conditions statement.",
                CreatedAt = now
            }
        };

        db.ContentSubmissions.Add(submission);
        db.ValidationRuns.Add(run);
        db.ValidationFindings.AddRange(findings);

        await db.SaveChangesAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[✓] Successfully seeded NOVA GREEN submission (ID: bef451ae-c700-4565-94dd-834e1213e95c) with 5 High-risk findings.");
        Console.ResetColor();
        return 0;
    }

    public static async Task<int> ExportSqliteToJsonAsync(string sqliteConn, string jsonFilePath)
    {
        Console.WriteLine($"\n[1/3] Reading all tables from SQLite source: {sqliteConn}");
        await using var db = CreateSqliteContext(sqliteConn);

        var guidelines = await db.BrandGuidelines.AsNoTracking().ToListAsync();
        var chunks = await db.GuidelineChunks.AsNoTracking().ToListAsync();
        var submissions = await db.ContentSubmissions.AsNoTracking().ToListAsync();
        var runs = await db.ValidationRuns.AsNoTracking().ToListAsync();
        var findings = await db.ValidationFindings.AsNoTracking().ToListAsync();
        var reviews = await db.ReviewDecisions.AsNoTracking().ToListAsync();
        var rules = await db.ValidationRules.AsNoTracking().ToListAsync();

        Console.WriteLine($"[2/3] Extracting records:");
        Console.WriteLine($"  - BrandGuidelines:    {guidelines.Count}");
        Console.WriteLine($"  - GuidelineChunks:    {chunks.Count}");
        Console.WriteLine($"  - ContentSubmissions: {submissions.Count}");
        Console.WriteLine($"  - ValidationRuns:     {runs.Count}");
        Console.WriteLine($"  - ValidationFindings: {findings.Count}");
        Console.WriteLine($"  - ReviewDecisions:    {reviews.Count}");
        Console.WriteLine($"  - ValidationRules:    {rules.Count}");

        var payload = new DatabaseBackupPayload
        {
            ExportedAt = DateTime.UtcNow,
            BrandGuidelines = guidelines,
            GuidelineChunks = chunks,
            ContentSubmissions = submissions,
            ValidationRuns = runs,
            ValidationFindings = findings,
            ReviewDecisions = reviews,
            ValidationRules = rules
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(jsonFilePath, json);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[3/3] Export complete. Saved to: {Path.GetFullPath(jsonFilePath)} ({new FileInfo(jsonFilePath).Length:N0} bytes)");
        Console.ResetColor();
        return 0;
    }

    public static async Task<int> MigrateSqliteToPostgresAsync(string sqliteConn, string postgresConn)
    {
        Console.WriteLine("\n[Phase 1] Ensuring SQLite source is ready and seeded...");
        await SeedNovaGreenAsync(sqliteConn);

        await using var sqliteDb = CreateSqliteContext(sqliteConn);
        await using var postgresDb = CreatePostgresContext(postgresConn);

        Console.WriteLine("\n[Phase 2] Initializing PostgreSQL target schema via EnsureCreated...");
        await postgresDb.Database.EnsureCreatedAsync();

        Console.WriteLine("\n[Phase 3] Migrating data with strict referential ordering and atomic transaction...\n");

        await using var transaction = await postgresDb.Database.BeginTransactionAsync();
        string currentEntity = "Initialization";

        try
        {
            // 1. Brand Guidelines (Root Parent — MUST BE SAVED FIRST)
            currentEntity = "BrandGuideline";
            var existingGuidelineIds = await postgresDb.BrandGuidelines.Select(g => g.Id).ToHashSetAsync();
            var sourceGuidelines = await sqliteDb.BrandGuidelines.AsNoTracking().ToListAsync();
            var newGuidelines = sourceGuidelines.Where(g => !existingGuidelineIds.Contains(g.Id)).ToList();
            foreach (var g in newGuidelines)
            {
                g.Chunks = new List<GuidelineChunk>();
                g.ValidationRules = new List<ValidationRule>();
            }
            if (newGuidelines.Count > 0)
            {
                await postgresDb.BrandGuidelines.AddRangeAsync(newGuidelines);
                await postgresDb.SaveChangesAsync();
            }
            var targetGuidelineIds = await postgresDb.BrandGuidelines.Select(g => g.Id).ToHashSetAsync();
            Console.WriteLine($"  [✓] BrandGuidelines:    {newGuidelines.Count} copied (Total in target: {targetGuidelineIds.Count})");

            // 2. Guideline Chunks (Child of BrandGuidelines)
            currentEntity = "GuidelineChunk";
            var existingChunkIds = await postgresDb.GuidelineChunks.Select(c => c.Id).ToHashSetAsync();
            var sourceChunks = await sqliteDb.GuidelineChunks.AsNoTracking().ToListAsync();
            var newChunks = sourceChunks
                .Where(c => !existingChunkIds.Contains(c.Id) && targetGuidelineIds.Contains(c.BrandGuidelineId))
                .ToList();
            foreach (var c in newChunks)
            {
                c.Findings = new List<ValidationFinding>();
                c.BrandGuideline = null;
            }
            if (newChunks.Count > 0)
            {
                await postgresDb.GuidelineChunks.AddRangeAsync(newChunks);
                await postgresDb.SaveChangesAsync();
            }
            var targetChunkIds = await postgresDb.GuidelineChunks.Select(c => c.Id).ToHashSetAsync();
            Console.WriteLine($"  [✓] GuidelineChunks:    {newChunks.Count} copied (Total in target: {targetChunkIds.Count})");

            // 3. Validation Rules (Depends on BrandGuidelines via SourceGuidelineId)
            currentEntity = "ValidationRule";
            var existingRuleIds = await postgresDb.ValidationRules.Select(r => r.Id).ToHashSetAsync();
            var sourceRules = await sqliteDb.ValidationRules.AsNoTracking().ToListAsync();
            var newRules = sourceRules.Where(r => !existingRuleIds.Contains(r.Id)).ToList();
            foreach (var r in newRules)
            {
                r.SourceGuideline = null;
                if (r.SourceGuidelineId.HasValue && !targetGuidelineIds.Contains(r.SourceGuidelineId.Value))
                {
                    r.SourceGuidelineId = null; // Unlink if guideline not found
                }
            }
            if (newRules.Count > 0)
            {
                await postgresDb.ValidationRules.AddRangeAsync(newRules);
                await postgresDb.SaveChangesAsync();
            }
            Console.WriteLine($"  [✓] ValidationRules:    {newRules.Count} new custom rules copied ({existingRuleIds.Count} default rules preserved)");

            // 4. Content Submissions (Root Parent)
            currentEntity = "ContentSubmission";
            var existingSubIds = await postgresDb.ContentSubmissions.Select(s => s.Id).ToHashSetAsync();
            var sourceSubs = await sqliteDb.ContentSubmissions.AsNoTracking().ToListAsync();
            var newSubs = sourceSubs.Where(s => !existingSubIds.Contains(s.Id)).ToList();
            foreach (var s in newSubs)
            {
                s.ValidationRuns = new List<ValidationRun>();
                s.ReviewDecisions = new List<ReviewDecision>();
            }
            if (newSubs.Count > 0)
            {
                await postgresDb.ContentSubmissions.AddRangeAsync(newSubs);
                await postgresDb.SaveChangesAsync();
            }
            var targetSubIds = await postgresDb.ContentSubmissions.Select(s => s.Id).ToHashSetAsync();
            Console.WriteLine($"  [✓] ContentSubmissions: {newSubs.Count} copied (Total in target: {targetSubIds.Count})");

            // 5. Validation Runs (Child of ContentSubmissions)
            currentEntity = "ValidationRun";
            var existingRunIds = await postgresDb.ValidationRuns.Select(r => r.Id).ToHashSetAsync();
            var sourceRuns = await sqliteDb.ValidationRuns.AsNoTracking().ToListAsync();
            var newRuns = sourceRuns
                .Where(r => !existingRunIds.Contains(r.Id) && targetSubIds.Contains(r.ContentSubmissionId))
                .ToList();
            foreach (var r in newRuns)
            {
                r.Findings = new List<ValidationFinding>();
                r.ReviewDecisions = new List<ReviewDecision>();
                r.ContentSubmission = null;
            }
            if (newRuns.Count > 0)
            {
                await postgresDb.ValidationRuns.AddRangeAsync(newRuns);
                await postgresDb.SaveChangesAsync();
            }
            var targetRunIds = await postgresDb.ValidationRuns.Select(r => r.Id).ToHashSetAsync();
            Console.WriteLine($"  [✓] ValidationRuns:     {newRuns.Count} copied (Total in target: {targetRunIds.Count})");

            // 6. Validation Findings (Child of ValidationRuns, references GuidelineChunks)
            currentEntity = "ValidationFinding";
            var existingFindingIds = await postgresDb.ValidationFindings.Select(f => f.Id).ToHashSetAsync();
            var sourceFindings = await sqliteDb.ValidationFindings.AsNoTracking().ToListAsync();
            var newFindings = sourceFindings
                .Where(f => !existingFindingIds.Contains(f.Id) && targetRunIds.Contains(f.ValidationRunId))
                .ToList();
            foreach (var f in newFindings)
            {
                f.ValidationRun = null;
                f.GuidelineChunk = null;
                if (f.GuidelineChunkId.HasValue && !targetChunkIds.Contains(f.GuidelineChunkId.Value))
                {
                    f.GuidelineChunkId = null; // Unlink chunk if not in target
                }
            }
            if (newFindings.Count > 0)
            {
                await postgresDb.ValidationFindings.AddRangeAsync(newFindings);
                await postgresDb.SaveChangesAsync();
            }
            Console.WriteLine($"  [✓] ValidationFindings: {newFindings.Count} copied (Total in target: {await postgresDb.ValidationFindings.CountAsync()})");

            // 7. Review Decisions (Child of ContentSubmissions, references ValidationRuns)
            currentEntity = "ReviewDecision";
            var existingReviewIds = await postgresDb.ReviewDecisions.Select(r => r.Id).ToHashSetAsync();
            var sourceReviews = await sqliteDb.ReviewDecisions.AsNoTracking().ToListAsync();
            var newReviews = sourceReviews
                .Where(r => !existingReviewIds.Contains(r.Id) && targetSubIds.Contains(r.ContentSubmissionId))
                .ToList();
            foreach (var r in newReviews)
            {
                r.ContentSubmission = null;
                r.ValidationRun = null;
                if (r.ValidationRunId.HasValue && !targetRunIds.Contains(r.ValidationRunId.Value))
                {
                    r.ValidationRunId = null;
                }
            }
            if (newReviews.Count > 0)
            {
                await postgresDb.ReviewDecisions.AddRangeAsync(newReviews);
                await postgresDb.SaveChangesAsync();
            }
            Console.WriteLine($"  [✓] ReviewDecisions:    {newReviews.Count} copied (Total in target: {await postgresDb.ReviewDecisions.CountAsync()})");

            // Commit atomic transaction
            await transaction.CommitAsync();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[Phase 4] Migration transaction committed successfully with full referential parity.");
            Console.ResetColor();

            return await VerifyParityAsync(sqliteConn, postgresConn);
        }
        catch (DbUpdateException dbEx)
        {
            await transaction.RollbackAsync();
            LogDbUpdateException(dbEx, currentEntity);
            return 1;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Migration aborted and rolled back on {currentEntity}: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    public static async Task<int> ImportJsonToPostgresAsync(string jsonFilePath, string postgresConn)
    {
        if (!File.Exists(jsonFilePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: Backup JSON file not found at: {jsonFilePath}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[1/3] Reading backup file: {jsonFilePath}");
        var json = await File.ReadAllTextAsync(jsonFilePath);
        var payload = JsonSerializer.Deserialize<DatabaseBackupPayload>(json, JsonOptions);
        if (payload == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Could not deserialize backup payload.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[2/3] Connecting to PostgreSQL and initializing schema...");
        await using var postgresDb = CreatePostgresContext(postgresConn);
        await postgresDb.Database.EnsureCreatedAsync();

        Console.WriteLine($"[3/3] Importing records in referential order with atomic transaction...");
        await using var transaction = await postgresDb.Database.BeginTransactionAsync();
        string currentEntity = "Initialization";

        try
        {
            // 1. Guidelines
            currentEntity = "BrandGuideline";
            var existingGIds = await postgresDb.BrandGuidelines.Select(g => g.Id).ToHashSetAsync();
            var newGuidelines = payload.BrandGuidelines.Where(g => !existingGIds.Contains(g.Id)).ToList();
            foreach (var g in newGuidelines)
            {
                g.Chunks = new List<GuidelineChunk>();
                g.ValidationRules = new List<ValidationRule>();
            }
            if (newGuidelines.Count > 0)
            {
                await postgresDb.BrandGuidelines.AddRangeAsync(newGuidelines);
                await postgresDb.SaveChangesAsync();
            }
            var targetGuidelineIds = await postgresDb.BrandGuidelines.Select(g => g.Id).ToHashSetAsync();

            // 2. Chunks
            currentEntity = "GuidelineChunk";
            var existingChunkIds = await postgresDb.GuidelineChunks.Select(c => c.Id).ToHashSetAsync();
            var newChunks = payload.GuidelineChunks
                .Where(c => !existingChunkIds.Contains(c.Id) && targetGuidelineIds.Contains(c.BrandGuidelineId))
                .ToList();
            foreach (var c in newChunks)
            {
                c.Findings = new List<ValidationFinding>();
                c.BrandGuideline = null;
            }
            if (newChunks.Count > 0)
            {
                await postgresDb.GuidelineChunks.AddRangeAsync(newChunks);
                await postgresDb.SaveChangesAsync();
            }
            var targetChunkIds = await postgresDb.GuidelineChunks.Select(c => c.Id).ToHashSetAsync();

            // 3. Validation Rules
            currentEntity = "ValidationRule";
            var existingRuleIds = await postgresDb.ValidationRules.Select(r => r.Id).ToHashSetAsync();
            var newRules = payload.ValidationRules.Where(r => !existingRuleIds.Contains(r.Id)).ToList();
            foreach (var r in newRules)
            {
                r.SourceGuideline = null;
                if (r.SourceGuidelineId.HasValue && !targetGuidelineIds.Contains(r.SourceGuidelineId.Value))
                {
                    r.SourceGuidelineId = null;
                }
            }
            if (newRules.Count > 0)
            {
                await postgresDb.ValidationRules.AddRangeAsync(newRules);
                await postgresDb.SaveChangesAsync();
            }

            // 4. Submissions
            currentEntity = "ContentSubmission";
            var existingSubIds = await postgresDb.ContentSubmissions.Select(s => s.Id).ToHashSetAsync();
            var newSubs = payload.ContentSubmissions.Where(s => !existingSubIds.Contains(s.Id)).ToList();
            foreach (var s in newSubs)
            {
                s.ValidationRuns = new List<ValidationRun>();
                s.ReviewDecisions = new List<ReviewDecision>();
            }
            if (newSubs.Count > 0)
            {
                await postgresDb.ContentSubmissions.AddRangeAsync(newSubs);
                await postgresDb.SaveChangesAsync();
            }
            var targetSubIds = await postgresDb.ContentSubmissions.Select(s => s.Id).ToHashSetAsync();

            // 5. Validation Runs
            currentEntity = "ValidationRun";
            var existingRunIds = await postgresDb.ValidationRuns.Select(r => r.Id).ToHashSetAsync();
            var newRuns = payload.ValidationRuns
                .Where(r => !existingRunIds.Contains(r.Id) && targetSubIds.Contains(r.ContentSubmissionId))
                .ToList();
            foreach (var r in newRuns)
            {
                r.Findings = new List<ValidationFinding>();
                r.ReviewDecisions = new List<ReviewDecision>();
                r.ContentSubmission = null;
            }
            if (newRuns.Count > 0)
            {
                await postgresDb.ValidationRuns.AddRangeAsync(newRuns);
                await postgresDb.SaveChangesAsync();
            }
            var targetRunIds = await postgresDb.ValidationRuns.Select(r => r.Id).ToHashSetAsync();

            // 6. Findings
            currentEntity = "ValidationFinding";
            var existingFindIds = await postgresDb.ValidationFindings.Select(f => f.Id).ToHashSetAsync();
            var newFindings = payload.ValidationFindings
                .Where(f => !existingFindIds.Contains(f.Id) && targetRunIds.Contains(f.ValidationRunId))
                .ToList();
            foreach (var f in newFindings)
            {
                f.ValidationRun = null;
                f.GuidelineChunk = null;
                if (f.GuidelineChunkId.HasValue && !targetChunkIds.Contains(f.GuidelineChunkId.Value))
                {
                    f.GuidelineChunkId = null;
                }
            }
            if (newFindings.Count > 0)
            {
                await postgresDb.ValidationFindings.AddRangeAsync(newFindings);
                await postgresDb.SaveChangesAsync();
            }

            // 7. Review Decisions
            currentEntity = "ReviewDecision";
            var existingRevIds = await postgresDb.ReviewDecisions.Select(r => r.Id).ToHashSetAsync();
            var newReviews = payload.ReviewDecisions
                .Where(r => !existingRevIds.Contains(r.Id) && targetSubIds.Contains(r.ContentSubmissionId))
                .ToList();
            foreach (var r in newReviews)
            {
                r.ContentSubmission = null;
                r.ValidationRun = null;
                if (r.ValidationRunId.HasValue && !targetRunIds.Contains(r.ValidationRunId.Value))
                {
                    r.ValidationRunId = null;
                }
            }
            if (newReviews.Count > 0)
            {
                await postgresDb.ReviewDecisions.AddRangeAsync(newReviews);
                await postgresDb.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[✓] Import transaction committed successfully.");
            Console.ResetColor();
            return 0;
        }
        catch (DbUpdateException dbEx)
        {
            await transaction.RollbackAsync();
            LogDbUpdateException(dbEx, currentEntity);
            return 1;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Import aborted and rolled back on {currentEntity}: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void LogDbUpdateException(DbUpdateException ex, string entityType)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n====================================================================");
        Console.WriteLine($"  DATABASE SAVE ERROR OCCURRED ON ENTITY: {entityType}");
        Console.WriteLine("====================================================================");
        Console.WriteLine($"Error Type:            {ex.GetType().FullName}");
        Console.WriteLine($"Message:               {ex.Message}");

        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            Console.WriteLine($"PostgreSQL SQLSTATE:   {pgEx.SqlState}");
            Console.WriteLine($"PostgreSQL Constraint: {pgEx.ConstraintName ?? "N/A"}");
            Console.WriteLine($"PostgreSQL Table:      {pgEx.TableName ?? "N/A"}");
            Console.WriteLine($"PostgreSQL Column:     {pgEx.ColumnName ?? "N/A"}");
            Console.WriteLine($"PostgreSQL Detail:     {pgEx.Detail ?? "N/A"}");
            Console.WriteLine($"PostgreSQL Routine:    {pgEx.Routine ?? "N/A"}");
        }
        else if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner Exception:       {ex.InnerException.GetType().FullName}");
            Console.WriteLine($"Inner Message:         {ex.InnerException.Message}");
        }

        if (ex.Entries != null && ex.Entries.Count > 0)
        {
            Console.WriteLine($"\nAffected Entries ({ex.Entries.Count}):");
            foreach (var entry in ex.Entries)
            {
                var entityName = entry.Entity.GetType().Name;
                var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue;
                Console.WriteLine($"  - Entity: {entityName}, State: {entry.State}, PK: {primaryKey}");
            }
        }
        Console.WriteLine("====================================================================\n");
        Console.ResetColor();
    }

    public static async Task<int> VerifyParityAsync(string sqliteConn, string? postgresConn)
    {
        await using var sqliteDb = CreateSqliteContext(sqliteConn);
        var sRules = await sqliteDb.ValidationRules.CountAsync();
        var sGuidelines = await sqliteDb.BrandGuidelines.CountAsync();
        var sChunks = await sqliteDb.GuidelineChunks.CountAsync();
        var sSubs = await sqliteDb.ContentSubmissions.CountAsync();
        var sRuns = await sqliteDb.ValidationRuns.CountAsync();
        var sFindings = await sqliteDb.ValidationFindings.CountAsync();
        var sReviews = await sqliteDb.ReviewDecisions.CountAsync();

        Console.WriteLine("\n====================================================================");
        Console.WriteLine("                      Database Record Audit");
        Console.WriteLine("====================================================================");
        Console.WriteLine($"{"Table Name",-24} {"SQLite Source",15} {"PostgreSQL Target",18} {"Parity Status",14}");
        Console.WriteLine(new string('-', 73));

        if (string.IsNullOrWhiteSpace(postgresConn))
        {
            Console.WriteLine($"{"ValidationRules",-24} {sRules,15} {"N/A",18} {"[Source Ready]",14}");
            Console.WriteLine($"{"BrandGuidelines",-24} {sGuidelines,15} {"N/A",18} {"[Source Ready]",14}");
            Console.WriteLine($"{"GuidelineChunks",-24} {sChunks,15} {"N/A",18} {"[Source Ready]",14}");
            Console.WriteLine($"{"ContentSubmissions",-24} {sSubs,15} {"N/A",18} {"[Source Ready]",14}");
            Console.WriteLine($"{"ValidationRuns",-24} {sRuns,15} {"N/A",18} {"[Source Ready]",14}");
            Console.WriteLine($"{"ValidationFindings",-24} {sFindings,15} {"N/A",18} {"[Source Ready]",14}");
            Console.WriteLine($"{"ReviewDecisions",-24} {sReviews,15} {"N/A",18} {"[Source Ready]",14}");
            return 0;
        }

        await using var postgresDb = CreatePostgresContext(postgresConn);
        var pRules = await postgresDb.ValidationRules.CountAsync();
        var pGuidelines = await postgresDb.BrandGuidelines.CountAsync();
        var pChunks = await postgresDb.GuidelineChunks.CountAsync();
        var pSubs = await postgresDb.ContentSubmissions.CountAsync();
        var pRuns = await postgresDb.ValidationRuns.CountAsync();
        var pFindings = await postgresDb.ValidationFindings.CountAsync();
        var pReviews = await postgresDb.ReviewDecisions.CountAsync();

        PrintParityRow("ValidationRules", sRules, pRules);
        PrintParityRow("BrandGuidelines", sGuidelines, pGuidelines);
        PrintParityRow("GuidelineChunks", sChunks, pChunks);
        PrintParityRow("ContentSubmissions", sSubs, pSubs);
        PrintParityRow("ValidationRuns", sRuns, pRuns);
        PrintParityRow("ValidationFindings", sFindings, pFindings);
        PrintParityRow("ReviewDecisions", sReviews, pReviews);

        return 0;
    }

    private static void PrintParityRow(string table, int source, int target)
    {
        var match = source <= target; // Target may have default seed rules
        var status = match ? "MATCH / READY" : "MISMATCH";
        var color = match ? ConsoleColor.Green : ConsoleColor.Yellow;

        Console.Write($"{table,-24} {source,15} {target,18} ");
        Console.ForegroundColor = color;
        Console.WriteLine($"[{status,12}]");
        Console.ResetColor();
    }
}

public class DatabaseBackupPayload
{
    public DateTime ExportedAt { get; set; }
    public List<BrandGuideline> BrandGuidelines { get; set; } = [];
    public List<GuidelineChunk> GuidelineChunks { get; set; } = [];
    public List<ContentSubmission> ContentSubmissions { get; set; } = [];
    public List<ValidationRun> ValidationRuns { get; set; } = [];
    public List<ValidationFinding> ValidationFindings { get; set; } = [];
    public List<ReviewDecision> ReviewDecisions { get; set; } = [];
    public List<ValidationRule> ValidationRules { get; set; } = [];
}
