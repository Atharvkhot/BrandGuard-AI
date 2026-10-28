using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Infrastructure.Ai;
using BrandGuardAI.Infrastructure.Chunking;
using BrandGuardAI.Infrastructure.Data;
using BrandGuardAI.Infrastructure.Pdf;
using BrandGuardAI.Infrastructure.Retrieval;
using BrandGuardAI.Infrastructure.Rules;
using BrandGuardAI.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrandGuardAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dbProvider = configuration["Database:Provider"] ?? "Auto";
        var sqlServerConn = configuration.GetConnectionString("DefaultConnection");
        var sqliteConn = configuration.GetConnectionString("SqliteConnection") ?? "Data Source=BrandGuardAI.db";

        bool useSqlite;

        if (string.Equals(dbProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(sqlServerConn))
            {
                throw new InvalidOperationException("Database:Provider is explicitly configured as 'SqlServer', but ConnectionStrings:DefaultConnection is empty or missing.");
            }
            useSqlite = false;
        }
        else if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            useSqlite = true;
        }
        else // "Auto"
        {
            useSqlite = true;
            if (!string.IsNullOrWhiteSpace(sqlServerConn))
            {
                try
                {
                    using var testConn = new SqlConnection(sqlServerConn);
                    testConn.Open();
                    useSqlite = false;
                }
                catch
                {
                    // SQL Server is unreachable, fall back to SQLite for seamless resilience
                    useSqlite = true;
                }
            }
        }

        // If SQLite is used, ensure the target folder exists (useful for container volumes e.g. /app/data)
        if (useSqlite)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(sqliteConn, @"Data Source=(?<path>[^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var dbPath = match.Groups["path"].Value.Trim();
                    var dir = Path.GetDirectoryName(dbPath);
                    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
            }
            catch
            {
                // Directory creation best-effort
            }
        }

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (useSqlite)
            {
                options.UseSqlite(sqliteConn);
            }
            else
            {
                options.UseSqlServer(sqlServerConn, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // HTTP Client
        services.AddHttpClient();

        // PDF & Text Extraction
        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();

        // Document Chunking
        services.AddSingleton<IDocumentChunker, DocumentChunker>();

        // Embedding & Retrieval
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();

        // Deterministic Rules & LLM Engine
        services.AddScoped<IDeterministicRuleEngine, DeterministicRuleEngine>();
        services.AddScoped<ILlmValidationService, LlmValidationService>();
        services.AddScoped<IValidationOrchestrator, ValidationOrchestrator>();

        // Application Services
        services.AddScoped<IGuidelineService, GuidelineService>();
        services.AddScoped<IContentSubmissionService, ContentSubmissionService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
