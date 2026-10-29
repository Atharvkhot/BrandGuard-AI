using BrandGuardAI.Infrastructure;
using BrandGuardAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandGuardAI.Tests;

public class PostgreSqlProviderTests
{
    // =========================================================================
    // 1. Standard ASP.NET Core Naming Convention (Database:Provider & ConnectionStrings:*)
    // =========================================================================

    [Fact]
    public void DependencyInjection_Supports_Standard_DatabaseProvider_PostgreSql()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Database:Provider", "PostgreSql" },
            { "ConnectionStrings:PostgreSqlConnection", "Host=localhost;Database=brandguard_test;Username=postgres;Password=test" }
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
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void DependencyInjection_Standard_Throws_When_PostgreSql_Provider_Configured_Without_ConnectionString()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Database:Provider", "PostgreSql" },
            { "ConnectionStrings:PostgreSqlConnection", "" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddInfrastructureServices(configuration);
        });

        Assert.Contains("neither ConnectionStrings:PostgreSqlConnection nor POSTGRES_CONNECTION_STRING was provided", ex.Message);
    }

    [Fact]
    public void DependencyInjection_Defaults_To_Sqlite_When_Provider_Is_Sqlite_Or_Unset()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "ConnectionStrings:SqliteConnection", "Data Source=:memory:" }
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
    public void DependencyInjection_Auto_Selects_PostgreSql_When_PostgreSqlConnection_Is_Provided()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Database:Provider", "Auto" },
            { "ConnectionStrings:PostgreSqlConnection", "Host=ep-test-123.neon.tech;Database=neondb;Username=user;Password=pass;SSL Mode=Require" }
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
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    // =========================================================================
    // 2. Render-Compatible Environment Variable Naming Convention
    //    (DATABASE_PROVIDER & POSTGRES_CONNECTION_STRING)
    // =========================================================================

    [Fact]
    public void DependencyInjection_Supports_Render_DATABASE_PROVIDER_And_POSTGRES_CONNECTION_STRING()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "DATABASE_PROVIDER", "PostgreSql" },
            { "POSTGRES_CONNECTION_STRING", "Host=ep-render-test.neon.tech;Database=neondb;Username=render_user;Password=render_pass;SSL Mode=Require" }
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
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("postgresql")]
    [InlineData("POSTGRESQL")]
    [InlineData("Postgres")]
    [InlineData("postgres")]
    public void DependencyInjection_Supports_Render_DATABASE_PROVIDER_Case_Variations(string providerValue)
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "DATABASE_PROVIDER", providerValue },
            { "POSTGRES_CONNECTION_STRING", "Host=ep-render-test.neon.tech;Database=neondb;Username=render_user;Password=render_pass;SSL Mode=Require" }
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
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void DependencyInjection_Render_Throws_When_DATABASE_PROVIDER_Is_PostgreSql_Without_ConnectionString()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "DATABASE_PROVIDER", "PostgreSql" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddInfrastructureServices(configuration);
        });

        Assert.Contains("neither ConnectionStrings:PostgreSqlConnection nor POSTGRES_CONNECTION_STRING was provided", ex.Message);
    }

    [Fact]
    public void DependencyInjection_Supports_Render_DATABASE_PROVIDER_Sqlite()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "DATABASE_PROVIDER", "Sqlite" },
            { "SQLITE_CONNECTION_STRING", "Data Source=:memory:" }
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
    public void DependencyInjection_Render_Auto_Selects_PostgreSql_When_POSTGRES_CONNECTION_STRING_Provided()
    {
        // Arrange - No DATABASE_PROVIDER specified, but POSTGRES_CONNECTION_STRING is present
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "DATABASE_PROVIDER", "Auto" },
            { "POSTGRES_CONNECTION_STRING", "Host=ep-render-test.neon.tech;Database=neondb;Username=render_user;Password=render_pass;SSL Mode=Require" }
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
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    // =========================================================================
    // 3. Mixed / Interchangeable Environment Variable Naming Conventions
    // =========================================================================

    [Fact]
    public void DependencyInjection_Supports_DATABASE_PROVIDER_With_ConnectionStrings_PostgreSqlConnection()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "DATABASE_PROVIDER", "PostgreSql" },
            { "ConnectionStrings:PostgreSqlConnection", "Host=localhost;Database=brandguard_test;Username=postgres;Password=test" }
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
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void DependencyInjection_Supports_Database_Provider_With_POSTGRES_CONNECTION_STRING()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Database:Provider", "PostgreSql" },
            { "POSTGRES_CONNECTION_STRING", "Host=localhost;Database=brandguard_test;Username=postgres;Password=test" }
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
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }
}
