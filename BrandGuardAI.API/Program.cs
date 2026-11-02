using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BrandGuardAI.Infrastructure;
using BrandGuardAI.Infrastructure.Data;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers with string enum conversion
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BrandGuard AI API",
        Version = "v1",
        Description = "AI-powered brand governance & marketing compliance validation platform. AI Detects. Evidence Explains. Humans Decide.",
        Contact = new OpenApiContact
        {
            Name = "TechDuo (Atharv Khot & Ishwari Kadam)"
        }
    });
});

// Configure Built-in Rate Limiting for Public Hackathon Demo
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var response = new
        {
            error = "Too Many Requests",
            message = "Rate limit exceeded. To protect the public hackathon demo, requests are temporarily throttled. Please wait a few seconds and try again.",
            statusCode = StatusCodes.Status429TooManyRequests
        };
        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
    };

    // Stricter limits requested by security spec:
    // 1. AI validation requests (10 per minute)
    options.AddPolicy("AiValidationPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetSafeClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 2. PDF guideline uploads (5 per minute)
    options.AddPolicy("GuidelineUploadPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetSafeClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 3. Demo seeding (5 per minute)
    options.AddPolicy("DemoSeedPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetSafeClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 4. Content creation (15 per minute)
    options.AddPolicy("ContentCreationPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetSafeClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 5. Review decision writes and batch review operations (25 per minute)
    options.AddPolicy("ReviewDecisionPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetSafeClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 25,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Register Infrastructure Services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Configure CORS for Angular Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var envAllowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]
            ?? builder.Configuration["CORS_ALLOWED_ORIGINS"]
            ?? Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
            ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");

        var configuredList = new List<string>();

        if (!string.IsNullOrWhiteSpace(envAllowedOrigins))
        {
            configuredList.AddRange(envAllowedOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        var configSectionOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (configSectionOrigins != null && configSectionOrigins.Length > 0)
        {
            configuredList.AddRange(configSectionOrigins);
        }

        // Always include known production Vercel frontend and local development ports
        configuredList.Add("https://brand-guard-ai-psi.vercel.app");
        configuredList.Add("http://localhost:4200");
        configuredList.Add("http://127.0.0.1:4200");
        configuredList.Add("http://localhost:3000");

        var sanitizedOrigins = configuredList
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        policy.WithOrigins(sanitizedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure Middleware Pipeline
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BrandGuard AI v1");
        c.RoutePrefix = "swagger";
    });
}

// Global Exception Handling Middleware (Secure - never leaks stack traces or credentials)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception processing request {Path}", context.Request.Path);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var isDev = app.Environment.IsDevelopment();
        var errorResponse = new Dictionary<string, object>
        {
            ["error"] = "An unexpected error occurred while processing the request."
        };

        if (isDev)
        {
            errorResponse["details"] = ex.Message;
        }

        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

// Health Check Endpoint (safe for external monitoring - does not expose connection strings or credentials)
app.MapGet("/health", async (ApplicationDbContext db) =>
{
    var providerName = db.Database.ProviderName ?? "Unknown";
    try
    {
        var rulesCount = await db.ValidationRules.CountAsync();
        return Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            database = "Connected",
            provider = providerName,
            rulesConfigured = rulesCount,
            version = "1.0.0"
        });
    }
    catch (Exception)
    {
        return Results.Json(new
        {
            status = "Degraded",
            timestamp = DateTime.UtcNow,
            database = "Unavailable",
            provider = providerName,
            error = "Database connectivity check failed."
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("HealthCheck");

// Ensure Database is Created & Seeded
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var providerName = dbContext.Database.ProviderName ?? "Unknown";

    try
    {
        logger.LogInformation("Database initialization starting. Active Provider: {Provider}", providerName);
        dbContext.Database.EnsureCreated();
        DatabaseSchemaMigrator.EnsureSchemaUpToDate(dbContext, logger);
        
        // Sync updated rule pattern if existing
        var superlativeRule = dbContext.ValidationRules.Find(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        if (superlativeRule != null && !superlativeRule.RuleValue.Contains("a-zA-Z"))
        {
            superlativeRule.RuleValue = @"\b(best(\s+[a-zA-Z]+)?\s+in\s+the\s+world|#1(\s+[a-zA-Z]+)?\s+in\s+the\s+world|unmatched|superior\s+to\s+all|unbeatable)\b";
            dbContext.SaveChanges();
            logger.LogInformation("Updated Superlative Claims Verification rule pattern in database.");
        }

        logger.LogInformation("Database verified and initialized successfully. Active Provider: {Provider}", providerName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database verification or initialization failed for Provider: {Provider}", providerName);
    }
}

// Ensure runtime directories exist
var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "uploads", "guidelines");
if (!Directory.Exists(uploadsDir))
{
    Directory.CreateDirectory(uploadsDir);
}

app.Run();

// Safe Client Identification helper for Rate Limiting
static string GetSafeClientPartitionKey(HttpContext httpContext)
{
    // Do NOT trust arbitrary client-supplied X-Forwarded-For headers directly to prevent spoofing and rate-limit bypass.
    // Rely exclusively on the peer socket connection IP resolved by the server/host runtime.
    return httpContext.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous-peer";
}

public partial class Program { }
