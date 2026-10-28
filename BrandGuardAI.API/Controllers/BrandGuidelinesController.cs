using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BrandGuardAI.API.Controllers;

[ApiController]
[Route("api/guidelines")]
public class BrandGuidelinesController : ControllerBase
{
    private readonly IGuidelineService _guidelineService;
    private readonly ILogger<BrandGuidelinesController> _logger;

    public BrandGuidelinesController(
        IGuidelineService guidelineService,
        ILogger<BrandGuidelinesController> logger)
    {
        _guidelineService = guidelineService;
        _logger = logger;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
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
        var deleted = await _guidelineService.DeleteGuidelineAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { error = $"Guideline with ID {id} not found." });
        }
        return NoContent();
    }
}
