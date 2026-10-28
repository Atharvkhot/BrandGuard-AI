using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BrandGuardAI.API.Controllers;

[ApiController]
[Route("api/content")]
public class ContentController : ControllerBase
{
    private readonly IContentSubmissionService _contentService;
    private readonly IValidationOrchestrator _validationOrchestrator;
    private readonly ILogger<ContentController> _logger;

    public ContentController(
        IContentSubmissionService contentService,
        IValidationOrchestrator validationOrchestrator,
        ILogger<ContentController> logger)
    {
        _contentService = contentService;
        _validationOrchestrator = validationOrchestrator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ContentSubmissionDto>> CreateSubmission(
        [FromBody] CreateContentSubmissionDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.ContentText))
        {
            return BadRequest(new { error = "Title and marketing content text are required." });
        }

        try
        {
            var result = await _contentService.CreateSubmissionAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetSubmissionById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<ContentSubmissionDto>>> GetAllSubmissions(CancellationToken cancellationToken)
    {
        var list = await _contentService.GetAllSubmissionsAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContentSubmissionDetailDto>> GetSubmissionById(Guid id, CancellationToken cancellationToken)
    {
        var submission = await _contentService.GetSubmissionByIdAsync(id, cancellationToken);
        if (submission == null)
        {
            return NotFound(new { error = $"Content submission {id} not found." });
        }
        return Ok(submission);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContentSubmissionDto>> UpdateSubmission(
        Guid id,
        [FromBody] UpdateContentSubmissionDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _contentService.UpdateSubmissionAsync(id, dto, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Content submission {id} not found." });
        }
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<ValidationResultDto>> ValidateContent(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _validationOrchestrator.RunValidationAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Content submission {id} not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing validation for submission {Id}", id);
            return StatusCode(500, new { error = "Validation process encountered an internal error: " + ex.Message });
        }
    }

    [HttpGet("{id:guid}/validation-result")]
    public async Task<ActionResult<ValidationResultDto>> GetLatestValidationResult(Guid id, CancellationToken cancellationToken)
    {
        var result = await _contentService.GetLatestValidationResultAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(new { error = $"No validation run found for submission {id}." });
        }
        return Ok(result);
    }
}
