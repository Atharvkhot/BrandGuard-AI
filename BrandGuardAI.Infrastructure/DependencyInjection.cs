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
        var dbProvider = configuration["DATABASE_PROVIDER"] 
            ?? configuration["Database:Provider"] 
            ?? "Sqlite";

        var sqlServerConn = configuration["SQLSERVER_CONNECTION_STRING"]
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings:DefaultConnection"];

        var sqliteConn = configuration["SQLITE_CONNECTION_STRING"]
            ?? configuration.GetConnectionString("SqliteConnection") 
            ?? configuration["ConnectionStrings:SqliteConnection"]
            ?? "Data Source=BrandGuardAI.db";

        var postgresConn = configuration["POSTGRES_CONNECTION_STRING"]
            ?? configuration["POSTGRESQL_CONNECTION_STRING"]
            ?? configuration.GetConnectionString("PostgreSqlConnection")
            ?? configuration["ConnectionStrings:PostgreSqlConnection"];

        string selectedProvider;

        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(postgresConn))
            {
                throw new InvalidOperationException("Database provider is configured as 'PostgreSql', but neither ConnectionStrings:PostgreSqlConnection nor POSTGRES_CONNECTION_STRING was provided.");
            }
            selectedProvider = "PostgreSql";
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }
        else if (string.Equals(dbProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(sqlServerConn))
            {
                throw new InvalidOperationException("Database:Provider is explicitly configured as 'SqlServer', but ConnectionStrings:DefaultConnection is empty or missing.");
            }
            selectedProvider = "SqlServer";
        }
        else if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            selectedProvider = "Sqlite";
        }
        else // "Auto" or any fallback
        {
            if (!string.IsNullOrWhiteSpace(postgresConn))
            {
                selectedProvider = "PostgreSql";
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            }
            else if (!string.IsNullOrWhiteSpace(sqlServerConn))
            {
                try
                {
                    using var testConn = new SqlConnection(sqlServerConn);
                    testConn.Open();
                    selectedProvider = "SqlServer";
                }
                catch
                {
                    selectedProvider = "Sqlite";
                }
            }
            else
            {
                selectedProvider = "Sqlite";
            }
        }

        // If SQLite is used, ensure the target folder exists (useful for container volumes e.g. /app/data)
        if (selectedProvider == "Sqlite")
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
            switch (selectedProvider)
            {
                case "PostgreSql":
                    options.UseNpgsql(postgresConn);
                    break;
                case "SqlServer":
                    options.UseSqlServer(sqlServerConn, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
                    break;
                case "Sqlite":
                default:
                    options.UseSqlite(sqliteConn);
                    break;
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
