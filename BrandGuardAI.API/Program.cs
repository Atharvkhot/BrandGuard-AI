using System.Text.Json.Serialization;
using BrandGuardAI.Infrastructure;
using BrandGuardAI.Infrastructure.Data;
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

// Register Infrastructure Services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Configure CORS for Angular Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins(
                    "http://localhost:4200",
                    "http://127.0.0.1:4200",
                    "http://localhost:3000",
                    "https://brand-guard-ai-psi.vercel.app")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Production: Restrict to configured origins (default to Vercel production frontend)
            var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "https://brand-guard-ai-psi.vercel.app" };

            var sanitizedOrigins = configuredOrigins
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            policy.WithOrigins(sanitizedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
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

// Global Exception Handling Middleware
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

        var errorResponse = new
        {
            error = "An unexpected error occurred while processing the request.",
            details = ex.Message
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Health Check Endpoint (does not require external AI call)
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
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "Degraded",
            timestamp = DateTime.UtcNow,
            database = "Unavailable",
            provider = providerName,
            error = ex.Message
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
