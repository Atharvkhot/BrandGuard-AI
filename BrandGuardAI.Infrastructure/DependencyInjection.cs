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

        bool useSqlite = string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase);

        if (!useSqlite && (string.Equals(dbProvider, "Auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(sqlServerConn)))
        {
            // Test if SQL Server is actually reachable
            try
            {
                if (!string.IsNullOrWhiteSpace(sqlServerConn))
                {
                    using var testConn = new SqlConnection(sqlServerConn);
                    testConn.Open();
                }
                else
                {
                    useSqlite = true;
                }
            }
            catch
            {
                // SQL Server unavailable on host machine - gracefully activate SQLite persistence
                useSqlite = true;
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
