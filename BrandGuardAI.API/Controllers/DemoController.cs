using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BrandGuardAI.API.Controllers;

[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    public static readonly Guid SeededMasterGuidelineId = Guid.Parse("c1b0789c-8bd6-4295-9f57-177ba65cf1e7");

    private readonly IGuidelineService _guidelineService;
    private readonly IContentSubmissionService _contentService;
    private readonly IValidationOrchestrator _validationOrchestrator;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DemoController> _logger;

    public DemoController(
        IGuidelineService guidelineService,
        IContentSubmissionService contentService,
        IValidationOrchestrator validationOrchestrator,
        IApplicationDbContext context,
        ILogger<DemoController> logger)
    {
        _guidelineService = guidelineService;
        _contentService = contentService;
        _validationOrchestrator = validationOrchestrator;
        _context = context;
        _logger = logger;
    }

    [HttpGet("sample-pdf")]
    public IActionResult DownloadSamplePdf()
    {
        var pdfBytes = SamplePdfGenerator.CreateSampleGuidelinePdf();
        return File(pdfBytes, "application/pdf", "BrandGuard_Master_Guidelines_2026.pdf");
    }

    [HttpGet("samples")]
    public IActionResult GetSamplePrompts()
    {
        return Ok(new
        {
            nonCompliantSample = new
            {
                title = "Spring Product Launch — Hero Campaign",
                contentType = "Social Media Post",
                content = "Our product guarantees 100% results and is the best solution in the world. Eliminate all friction with our revolutionary platform today!"
            },
            compliantSample = new
            {
                title = "Enterprise Governance Solution Overview",
                contentType = "Website Content",
                content = "BrandGuard AI provides comprehensive brand policy governance for enterprise marketing operations. Designed to streamline editorial review cycles and align communication with approved brand standards. Terms and conditions apply. Results may vary by operational scale."
            }
        });
    }

    [HttpPost("seed")]
    [EnableRateLimiting("DemoSeedPolicy")]
    public async Task<IActionResult> SeedDemoEnvironment(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seeding demo environment for judge demonstration...");

        // 1. Ensure master guideline exists using robust multi-identifier check
        var existingGuideline = await _context.BrandGuidelines
            .Include(g => g.Chunks)
            .FirstOrDefaultAsync(g => g.Id == SeededMasterGuidelineId ||
                                      g.FileName == "BrandGuard_Master_Guidelines_2026.pdf" ||
                                      g.Title.Contains("BrandGuard Master"), cancellationToken);

        GuidelineDto guidelineDto;
        if (existingGuideline == null)
        {
            var pdfBytes = SamplePdfGenerator.CreateSampleGuidelinePdf();
            using var stream = new MemoryStream(pdfBytes);
            guidelineDto = await _guidelineService.UploadGuidelineAsync(
                "BrandGuard Master Brand Guidelines 2026",
                "BrandGuard_Master_Guidelines_2026.pdf",
                stream,
                cancellationToken);
        }
        else
        {
            guidelineDto = new GuidelineDto(
                existingGuideline.Id,
                existingGuideline.Title,
                existingGuideline.FileName,
                existingGuideline.Status,
                existingGuideline.UploadedAt,
                existingGuideline.IsActive,
                existingGuideline.Chunks.Count,
                existingGuideline.ExtractedText.Length
            );
        }

        // 2. Check if sample submissions exist
        var existingViolating = await _context.ContentSubmissions
            .FirstOrDefaultAsync(s => s.Title == "Spring Product Launch — Hero Campaign", cancellationToken);

        var existingCompliant = await _context.ContentSubmissions
            .FirstOrDefaultAsync(s => s.Title == "Enterprise Governance Solution Overview", cancellationToken);

        // Fully idempotent: if both samples already exist, never duplicate records
        if (existingViolating != null && existingCompliant != null)
        {
            return Ok(new
            {
                message = "Demo environment already initialized.",
                guideline = guidelineDto
            });
        }

        ValidationResultDto? activeValidation = null;

        if (existingViolating == null)
        {
            // Create violating submission
            var violating = await _contentService.CreateSubmissionAsync(new CreateContentSubmissionDto(
                "Spring Product Launch — Hero Campaign",
                "Social Media Post",
                "Our product guarantees 100% results and is the best solution in the world. Eliminate all friction with our revolutionary platform today!"
            ), cancellationToken);

            // Execute validation
            activeValidation = await _validationOrchestrator.RunValidationAsync(violating.Id, cancellationToken);
        }

        if (existingCompliant == null)
        {
            // Create compliant submission
            await _contentService.CreateSubmissionAsync(new CreateContentSubmissionDto(
                "Enterprise Governance Solution Overview",
                "Website Content",
                "BrandGuard AI provides comprehensive brand policy governance for enterprise marketing operations. Designed to streamline editorial review cycles and align communication with approved brand standards. Terms and conditions apply. Results may vary."
            ), cancellationToken);
        }

        return Ok(new
        {
            message = "Demo environment successfully seeded with guidelines, sample content, and live validation results.",
            guideline = guidelineDto,
            activeValidation = activeValidation
        });
    }
}
