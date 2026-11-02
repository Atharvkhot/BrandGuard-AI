using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BrandGuardAI.API.Controllers;

[ApiController]
[Route("api/guidelines")]
public class BrandGuidelinesController : ControllerBase
{
    public static readonly Guid SeededMasterGuidelineId = Guid.Parse("c1b0789c-8bd6-4295-9f57-177ba65cf1e7");

    private readonly IGuidelineService _guidelineService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BrandGuidelinesController> _logger;

    public BrandGuidelinesController(
        IGuidelineService guidelineService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<BrandGuidelinesController> logger)
    {
        _guidelineService = guidelineService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("GuidelineUploadPolicy")]
    public async Task<ActionResult<GuidelineDto>> UploadGuideline(
        [FromForm] IFormFile file,
        [FromForm] string? title,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded or file is empty." });
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only PDF files are supported for Brand Guidelines." });
        }

        if (file.Length > 25 * 1024 * 1024)
        {
            return BadRequest(new { error = "File exceeds the 25MB maximum limit." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _guidelineService.UploadGuidelineAsync(
                title ?? Path.GetFileNameWithoutExtension(file.FileName),
                file.FileName,
                stream,
                cancellationToken);

            return CreatedAtAction(nameof(GetGuidelineById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing PDF upload");
            return StatusCode(500, new { error = "Failed to process and index the brand guideline PDF." });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<GuidelineDto>>> GetAllGuidelines(CancellationToken cancellationToken)
    {
        var list = await _guidelineService.GetAllGuidelinesAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GuidelineDetailDto>> GetGuidelineById(Guid id, CancellationToken cancellationToken)
    {
        var guideline = await _guidelineService.GetGuidelineByIdAsync(id, cancellationToken);
        if (guideline == null)
        {
            return NotFound(new { error = $"Guideline with ID {id} not found." });
        }
        return Ok(guideline);
    }

    [HttpGet("{id:guid}/chunks")]
    public async Task<ActionResult<List<GuidelineChunkDto>>> GetGuidelineChunks(Guid id, CancellationToken cancellationToken)
    {
        var chunks = await _guidelineService.GetGuidelineChunksAsync(id, cancellationToken);
        return Ok(chunks);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGuideline(Guid id, CancellationToken cancellationToken)
    {
        // 1. Never allow deleting the seeded master brand guideline by well-known GUID
        if (id == SeededMasterGuidelineId)
        {
            _logger.LogWarning("Blocked destructive operation: Attempted deletion of seeded master brand guideline by ID {Id}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Forbidden",
                message = "The seeded master brand guideline is protected and cannot be deleted."
            });
        }

        var guideline = await _guidelineService.GetGuidelineByIdAsync(id, cancellationToken);
        if (guideline == null)
        {
            return NotFound(new { error = $"Guideline with ID {id} not found." });
        }

        // 2. Identify if target guideline represents the seeded master guideline by filename or title
        bool isSeededRecord = guideline.Id == SeededMasterGuidelineId ||
                              string.Equals(guideline.FileName, "BrandGuard_Master_Guidelines_2026.pdf", StringComparison.OrdinalIgnoreCase) ||
                              guideline.Title.Contains("BrandGuard Master", StringComparison.OrdinalIgnoreCase);

        if (isSeededRecord)
        {
            _logger.LogWarning("Blocked destructive operation: Attempted deletion of seeded master brand guideline '{Title}' ({Id})", guideline.Title, id);
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Forbidden",
                message = "Seeded master brand guidelines are protected from deletion in all environments."
            });
        }

        // 3. In production demo mode, disable destructive guideline deletion for anonymous users
        var isDemoMode = _configuration.GetValue<bool?>("DEMO_MODE") ?? !_environment.IsDevelopment();
        var adminSecret = _configuration["ADMIN_SECRET_KEY"] ?? Environment.GetEnvironmentVariable("ADMIN_SECRET_KEY");
        var clientAdminKey = Request.Headers["X-Admin-Key"].FirstOrDefault();
        bool isAuthorizedAdmin = !string.IsNullOrEmpty(adminSecret) && string.Equals(adminSecret, clientAdminKey, StringComparison.Ordinal);

        if (isDemoMode && !isAuthorizedAdmin)
        {
            _logger.LogWarning("Anonymous user blocked from deleting guideline {Id} in public demo mode.", id);
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Forbidden",
                message = "Destructive guideline deletion is disabled in public demo mode to preserve shared evaluation state for hackathon judges."
            });
        }

        var deleted = await _guidelineService.DeleteGuidelineAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { error = $"Guideline with ID {id} not found." });
        }
        return NoContent();
    }
}
