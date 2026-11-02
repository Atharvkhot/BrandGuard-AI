using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Data;

public static class DatabaseSchemaMigrator
{
    public static void EnsureSchemaUpToDate(ApplicationDbContext context, ILogger logger)
    {
        var provider = context.Database.ProviderName ?? string.Empty;
        bool isPostgres = provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                          provider.Contains("PostgreSql", StringComparison.OrdinalIgnoreCase);
        bool isSqlite = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isPostgres)
            {
                ApplyPostgresSchemaUpdates(context, logger);
            }
            else if (isSqlite)
            {
                ApplySqliteSchemaUpdates(context, logger);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while executing safe schema verification for provider {Provider}", provider);
            // Non-fatal to prevent crashing if already applied or permissions restricted
        }
    }

    private static void ApplyPostgresSchemaUpdates(ApplicationDbContext context, ILogger logger)
    {
        // Safe, non-destructive column additions with IF NOT EXISTS
        var sql = @"
            ALTER TABLE ""ValidationFindings"" ADD COLUMN IF NOT EXISTS ""IsAcknowledged"" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE ""ValidationFindings"" ADD COLUMN IF NOT EXISTS ""AcknowledgedAt"" timestamp without time zone NULL;
            ALTER TABLE ""ValidationFindings"" ADD COLUMN IF NOT EXISTS ""AcknowledgedBy"" character varying(150) NULL;
        ";
        context.Database.ExecuteSqlRaw(sql);
        logger.LogInformation("Safe PostgreSQL schema check completed for ValidationFindings acknowledgment columns.");
    }

    private static void ApplySqliteSchemaUpdates(ApplicationDbContext context, ILogger logger)
    {
        // Check existing columns in SQLite via PRAGMA table_info
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA table_info(ValidationFindings);";
        context.Database.OpenConnection();

        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var colName = reader["name"]?.ToString();
                if (!string.IsNullOrEmpty(colName))
                {
                    existingColumns.Add(colName);
                }
            }
        }

        if (!existingColumns.Contains("IsAcknowledged"))
        {
            context.Database.ExecuteSqlRaw(@"ALTER TABLE ""ValidationFindings"" ADD COLUMN ""IsAcknowledged"" INTEGER NOT NULL DEFAULT 0;");
            logger.LogInformation("Added 'IsAcknowledged' column to SQLite ValidationFindings.");
        }

        if (!existingColumns.Contains("AcknowledgedAt"))
        {
            context.Database.ExecuteSqlRaw(@"ALTER TABLE ""ValidationFindings"" ADD COLUMN ""AcknowledgedAt"" TEXT NULL;");
            logger.LogInformation("Added 'AcknowledgedAt' column to SQLite ValidationFindings.");
        }

        if (!existingColumns.Contains("AcknowledgedBy"))
        {
            context.Database.ExecuteSqlRaw(@"ALTER TABLE ""ValidationFindings"" ADD COLUMN ""AcknowledgedBy"" TEXT NULL;");
            logger.LogInformation("Added 'AcknowledgedBy' column to SQLite ValidationFindings.");
        }
    }
}
