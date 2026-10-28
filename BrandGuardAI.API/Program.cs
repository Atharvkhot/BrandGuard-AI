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
        policy.SetIsOriginAllowed(_ => true)
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

// Ensure Database is Created & Seeded
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        
        // Sync updated rule pattern if existing
        var superlativeRule = dbContext.ValidationRules.Find(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        if (superlativeRule != null && !superlativeRule.RuleValue.Contains("a-zA-Z"))
        {
            superlativeRule.RuleValue = @"\b(best(\s+[a-zA-Z]+)?\s+in\s+the\s+world|#1(\s+[a-zA-Z]+)?\s+in\s+the\s+world|unmatched|superior\s+to\s+all|unbeatable)\b";
            dbContext.SaveChanges();
            logger.LogInformation("Updated Superlative Claims Verification rule pattern in database.");
        }

        logger.LogInformation("Database verified and initialized successfully.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Initial SQL Server connection failed. Attempting SQLite local storage fallback.");
    }
}

app.Run();
