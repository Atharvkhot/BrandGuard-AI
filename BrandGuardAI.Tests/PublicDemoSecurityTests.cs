using BrandGuardAI.API.Controllers;
using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Ai;
using BrandGuardAI.Infrastructure.Chunking;
using BrandGuardAI.Infrastructure.Data;
using BrandGuardAI.Infrastructure.Pdf;
using BrandGuardAI.Infrastructure.Retrieval;
using BrandGuardAI.Infrastructure.Rules;
using BrandGuardAI.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using System.Threading.RateLimiting;
using Xunit;

namespace BrandGuardAI.Tests;

public class PublicDemoSecurityTests
{
    private (ApplicationDbContext Context, GuidelineService GuidelineSvc, ContentSubmissionService ContentSvc, ValidationOrchestrator Orchestrator, ReviewService ReviewSvc) SetupDatabaseAndServices()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();

        var pdfExtractor = new PdfPigTextExtractor(NullLogger<PdfPigTextExtractor>.Instance);
        var chunker = new DocumentChunker();
        var embeddingSvc = new EmbeddingService(httpClient, config, NullLogger<EmbeddingService>.Instance);
        var searchSvc = new SemanticSearchService(context, embeddingSvc, NullLogger<SemanticSearchService>.Instance);
        var ruleEngine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var llmSvc = new LlmValidationService(httpClient, config, NullLogger<LlmValidationService>.Instance);

        var guidelineSvc = new GuidelineService(context, pdfExtractor, chunker, embeddingSvc, NullLogger<GuidelineService>.Instance);
        var contentSvc = new ContentSubmissionService(context, NullLogger<ContentSubmissionService>.Instance);
        var orchestrator = new ValidationOrchestrator(context, ruleEngine, searchSvc, llmSvc, NullLogger<ValidationOrchestrator>.Instance);
        var reviewSvc = new ReviewService(context, NullLogger<ReviewService>.Instance);

        return (context, guidelineSvc, contentSvc, orchestrator, reviewSvc);
    }

    [Fact]
    public async Task DeleteGuideline_WhenAttemptingToDeleteSeededMasterGuidelineById_ShouldReturn403Forbidden()
    {
        var (context, guidelineSvc, _, _, _) = SetupDatabaseAndServices();

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"DEMO_MODE", "false"}
        }).Build();

        var controller = new BrandGuidelinesController(
            guidelineSvc,
            envMock.Object,
            config,
            NullLogger<BrandGuidelinesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.DeleteGuideline(BrandGuidelinesController.SeededMasterGuidelineId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task DeleteGuideline_WhenTargetIsSeededMasterGuidelineByFilenameOrTitle_ShouldReturn403Forbidden()
    {
        var (context, guidelineSvc, _, _, _) = SetupDatabaseAndServices();

        var seededGuideline = new BrandGuideline
        {
            Id = Guid.NewGuid(),
            Title = "BrandGuard Master Brand Guidelines 2026",
            FileName = "BrandGuard_Master_Guidelines_2026.pdf",
            FilePath = "dummy/path.pdf",
            ExtractedText = "Master content"
        };
        context.BrandGuidelines.Add(seededGuideline);
        await context.SaveChangesAsync();

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"DEMO_MODE", "false"}
        }).Build();

        var controller = new BrandGuidelinesController(
            guidelineSvc,
            envMock.Object,
            config,
            NullLogger<BrandGuidelinesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.DeleteGuideline(seededGuideline.Id, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task DeleteGuideline_WhenInProductionDemoModeWithoutAdminKey_ShouldBlockAnonymousDeletion()
    {
        var (context, guidelineSvc, _, _, _) = SetupDatabaseAndServices();

        var customGuideline = new BrandGuideline
        {
            Id = Guid.NewGuid(),
            Title = "Custom Marketing Guide",
            FileName = "Custom_Guide.pdf",
            FilePath = "dummy/custom.pdf",
            ExtractedText = "Custom text"
        };
        context.BrandGuidelines.Add(customGuideline);
        await context.SaveChangesAsync();

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"DEMO_MODE", "true"},
            {"ADMIN_SECRET_KEY", "prod-admin-secret-2026"}
        }).Build();

        var controller = new BrandGuidelinesController(
            guidelineSvc,
            envMock.Object,
            config,
            NullLogger<BrandGuidelinesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.DeleteGuideline(customGuideline.Id, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // Verify record is preserved in database
        var stillExists = await context.BrandGuidelines.AnyAsync(g => g.Id == customGuideline.Id);
        stillExists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteGuideline_WhenInProductionDemoModeWithValidAdminKey_ShouldAllowDeletionOfNonMasterGuideline()
    {
        var (context, guidelineSvc, _, _, _) = SetupDatabaseAndServices();

        var customGuideline = new BrandGuideline
        {
            Id = Guid.NewGuid(),
            Title = "Temporary Test Document",
            FileName = "Temp_Doc.pdf",
            FilePath = "dummy/temp.pdf",
            ExtractedText = "Temporary text"
        };
        context.BrandGuidelines.Add(customGuideline);
        await context.SaveChangesAsync();

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"DEMO_MODE", "true"},
            {"ADMIN_SECRET_KEY", "prod-admin-secret-2026"}
        }).Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Admin-Key"] = "prod-admin-secret-2026";

        var controller = new BrandGuidelinesController(
            guidelineSvc,
            envMock.Object,
            config,
            NullLogger<BrandGuidelinesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.DeleteGuideline(customGuideline.Id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var exists = await context.BrandGuidelines.AnyAsync(g => g.Id == customGuideline.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DemoSeeding_WhenInvokedRepeatedly_ShouldBeStrictlyIdempotent()
    {
        var (context, guidelineSvc, contentSvc, orchestrator, _) = SetupDatabaseAndServices();

        var controller = new DemoController(
            guidelineSvc,
            contentSvc,
            orchestrator,
            context,
            NullLogger<DemoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // 1. First seeding invocation
        var firstResult = await controller.SeedDemoEnvironment(CancellationToken.None);
        firstResult.Should().BeOfType<OkObjectResult>();

        var guidelineCountAfterFirst = await context.BrandGuidelines.CountAsync();
        var submissionCountAfterFirst = await context.ContentSubmissions.CountAsync();

        guidelineCountAfterFirst.Should().Be(1);
        submissionCountAfterFirst.Should().Be(2);

        // 2. Second seeding invocation (immediate repeat)
        var secondResult = await controller.SeedDemoEnvironment(CancellationToken.None);
        secondResult.Should().BeOfType<OkObjectResult>();

        var secondObj = (OkObjectResult)secondResult;
        secondObj.Value.Should().NotBeNull();
        var json = JsonSerializer.Serialize(secondObj.Value);
        json.Should().Contain("Demo environment already initialized.");

        // 3. Verify record counts did NOT double
        var guidelineCountAfterSecond = await context.BrandGuidelines.CountAsync();
        var submissionCountAfterSecond = await context.ContentSubmissions.CountAsync();

        guidelineCountAfterSecond.Should().Be(guidelineCountAfterFirst);
        submissionCountAfterSecond.Should().Be(submissionCountAfterFirst);
    }

    [Fact]
    public void RateLimiter_AiValidationPolicy_ShouldThrottleAfterExceedingPermitLimit()
    {
        // 10 permits per 1 minute window
        var partition = RateLimitPartition.GetFixedWindowLimiter(
            "198.51.100.1",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

        using var limiter = partition.Factory("198.51.100.1");

        // First 10 requests acquire successfully
        for (int i = 0; i < 10; i++)
        {
            using var lease = limiter.AttemptAcquire(1);
            lease.IsAcquired.Should().BeTrue($"Permit {i + 1} should be acquired within limit");
        }

        // 11th request exceeds limit and is rejected (triggers HTTP 429)
        using var rejectedLease = limiter.AttemptAcquire(1);
        rejectedLease.IsAcquired.Should().BeFalse("11th request must be rejected under 10/min rate limit");
    }

    [Fact]
    public void RateLimiter_GuidelineUploadPolicy_ShouldThrottleAfterExceedingPermitLimit()
    {
        // 5 uploads per 1 minute window
        var partition = RateLimitPartition.GetFixedWindowLimiter(
            "198.51.100.2",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

        using var limiter = partition.Factory("198.51.100.2");

        for (int i = 0; i < 5; i++)
        {
            using var lease = limiter.AttemptAcquire(1);
            lease.IsAcquired.Should().BeTrue();
        }

        using var rejectedLease = limiter.AttemptAcquire(1);
        rejectedLease.IsAcquired.Should().BeFalse("6th upload in 1 minute must be rejected");
    }

    [Fact]
    public async Task ReviewWorkflows_WithSelfReportedReviewerName_ShouldPersistAuditDecisionAndRejectEmptyName()
    {
        var (context, _, contentSvc, _, reviewSvc) = SetupDatabaseAndServices();

        var submission = await contentSvc.CreateSubmissionAsync(new CreateContentSubmissionDto(
            "Audit Integrity Test Campaign",
            "Email",
            "Compliant marketing message adhering to guidelines."
        ));

        var controller = new ReviewsController(reviewSvc, NullLogger<ReviewsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // 1. Submit review with empty reviewer name -> Should fail
        var emptyNameDto = new SubmitReviewDecisionDto(
            submission.Id,
            null,
            "",
            ReviewDecisionType.Approved,
            "Looks good"
        );
        var badResult = await controller.SubmitDecision(emptyNameDto, CancellationToken.None);
        badResult.Result.Should().BeOfType<BadRequestObjectResult>();

        // 2. Submit review with valid self-reported reviewer name -> Should succeed
        var validDto = new SubmitReviewDecisionDto(
            submission.Id,
            null,
            "Dr. Hackathon Judge (External Auditor)",
            ReviewDecisionType.Approved,
            "Approved for production distribution."
        );
        var okResult = await controller.SubmitDecision(validDto, CancellationToken.None);
        okResult.Result.Should().BeOfType<OkObjectResult>();

        var responseDto = ((OkObjectResult)okResult.Result).Value as ReviewDecisionDto;
        responseDto.Should().NotBeNull();
        responseDto!.ReviewerName.Should().Be("Dr. Hackathon Judge (External Auditor)");
        responseDto.Decision.Should().Be(ReviewDecisionType.Approved);

        // 3. Verify database status
        var updatedSub = await context.ContentSubmissions.FindAsync(submission.Id);
        updatedSub!.Status.Should().Be(ContentSubmissionStatus.Approved);
    }
}
