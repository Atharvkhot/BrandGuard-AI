using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BrandGuardAI.API.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        IReviewService reviewService,
        ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ReviewDecisionDto>> SubmitDecision(
        [FromBody] SubmitReviewDecisionDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.ReviewerName))
        {
            return BadRequest(new { error = "Reviewer name is required." });
        }

        try
        {
            var result = await _reviewService.SubmitDecisionAsync(dto, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving review decision for submission {Id}", dto.ContentSubmissionId);
            return StatusCode(500, new { error = "Failed to save human review decision." });
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<ReviewHistoryItemDto>>> GetReviewHistory(CancellationToken cancellationToken)
    {
        var history = await _reviewService.GetReviewHistoryAsync(cancellationToken);
        return Ok(history);
    }

    [HttpGet("submission/{submissionId:guid}")]
    public async Task<ActionResult<ReviewDecisionDto>> GetDecisionBySubmissionId(Guid submissionId, CancellationToken cancellationToken)
    {
        var decision = await _reviewService.GetDecisionBySubmissionIdAsync(submissionId, cancellationToken);
        if (decision == null)
        {
            return NotFound(new { error = $"No review decision found for submission {submissionId}." });
        }
        return Ok(decision);
    }
}
