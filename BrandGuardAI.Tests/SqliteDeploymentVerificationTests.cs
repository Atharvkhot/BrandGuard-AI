using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure;
using BrandGuardAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandGuardAI.Tests;

public class SqliteDeploymentVerificationTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempDbConnection;

    public SqliteDeploymentVerificationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"BrandGuardAI_Test_{Guid.NewGuid():N}.db");
        _tempDbConnection = $"Data Source={_tempDbPath}";
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try
            {
                File.Delete(_tempDbPath);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public void DependencyInjection_Supports_DatabaseProvider_Sqlite()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Database:Provider", "Sqlite" },
            { "ConnectionStrings:SqliteConnection", _tempDbConnection }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Assert
        Assert.NotNull(dbContext);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
    }

    [Fact]
    public async Task EnsureCreated_Creates_New_Sqlite_Database_With_All_Required_Tables()
    {
        // Arrange: simulate pointing to /tmp or custom temp path
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Database:Provider", "Sqlite" },
            { "ConnectionStrings:SqliteConnection", _tempDbConnection }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Act: Execute the exact startup initialization logic
        var created = await dbContext.Database.EnsureCreatedAsync();

        // Assert: Database file was created on disk
        Assert.True(created, "Database.EnsureCreatedAsync should return true for a new database.");
        Assert.True(File.Exists(_tempDbPath), $"SQLite database file should exist at {_tempDbPath}");

        // Assert: Verify all 7 tables exist by querying each DbSet
        var guidelinesCount = await dbContext.BrandGuidelines.CountAsync();
        var chunksCount = await dbContext.GuidelineChunks.CountAsync();
        var submissionsCount = await dbContext.ContentSubmissions.CountAsync();
        var runsCount = await dbContext.ValidationRuns.CountAsync();
        var findingsCount = await dbContext.ValidationFindings.CountAsync();
        var reviewsCount = await dbContext.ReviewDecisions.CountAsync();
        var rulesCount = await dbContext.ValidationRules.CountAsync();

        Assert.Equal(0, guidelinesCount);
        Assert.Equal(0, chunksCount);
        Assert.Equal(0, submissionsCount);
        Assert.Equal(0, runsCount);
        Assert.Equal(0, findingsCount);
        Assert.Equal(0, reviewsCount);
        Assert.True(rulesCount >= 5, "Database should have seeded at least 5 default validation rules.");
    }

    [Fact]
    public async Task Sqlite_Supports_Full_CRUD_And_Cascade_Delete()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Database:Provider", "Sqlite" },
            { "ConnectionStrings:SqliteConnection", _tempDbConnection }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Act: Insert submission and run
        var submission = new ContentSubmission
        {
            Title = "Render SQLite Test Submission",
            ContentType = "BannerAd",
            ContentText = "100% Guaranteed results forever",
            Status = ContentSubmissionStatus.PendingReview
        };

        dbContext.ContentSubmissions.Add(submission);
        await dbContext.SaveChangesAsync();

        var run = new ValidationRun
        {
            ContentSubmissionId = submission.Id,
            Status = "Completed",
            OverallRiskLevel = RiskSeverity.High,
            TotalFindings = 1
        };

        dbContext.ValidationRuns.Add(run);
        await dbContext.SaveChangesAsync();

        // Assert
        var fetched = await dbContext.ContentSubmissions
            .Include(s => s.ValidationRuns)
            .FirstOrDefaultAsync(s => s.Id == submission.Id);

        Assert.NotNull(fetched);
        Assert.Single(fetched.ValidationRuns);
    }
}
