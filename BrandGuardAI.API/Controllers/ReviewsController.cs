using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    /// <summary>
    /// Submits a human review decision for a content submission.
    /// Note: In this public hackathon demo, ReviewerName is an unauthenticated, self-reported audit label.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("ReviewDecisionPolicy")]
    public async Task<ActionResult<ReviewDecisionDto>> SubmitDecision(
        [FromBody] SubmitReviewDecisionDto dto,
        CancellationToken cancellationToken)
    {
        if (dto == null)
        {
            return BadRequest(new { error = "Review decision payload is required." });
        }

        if (string.IsNullOrWhiteSpace(dto.ReviewerName))
        {
            return BadRequest(new { error = "Reviewer name is required (self-reported display name for demo audit history)." });
        }

        if (dto.ContentSubmissionId == Guid.Empty)
        {
            return BadRequest(new { error = "Valid content submission ID is required." });
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

    /// <summary>
    /// Acknowledges findings in batch.
    /// Note: AcknowledgedBy is a self-reported display name for demo audit history.
    /// </summary>
    [HttpPost("findings/acknowledge")]
    [EnableRateLimiting("ReviewDecisionPolicy")]
    public async Task<ActionResult<AcknowledgeFindingsResultDto>> AcknowledgeFindings(
        [FromBody] AcknowledgeFindingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.FindingIds == null || request.FindingIds.Count == 0)
        {
            return BadRequest(new { error = "At least one finding ID must be provided." });
        }

        try
        {
            var result = await _reviewService.AcknowledgeFindingsAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging validation findings in batch.");
            return StatusCode(500, new { error = "Failed to record finding acknowledgments." });
        }
    }

    /// <summary>
    /// Applies batch review decisions to multiple submissions.
    /// Note: ReviewerName is a self-reported display name for demo audit history.
    /// </summary>
    [HttpPost("batch")]
    [EnableRateLimiting("ReviewDecisionPolicy")]
    public async Task<ActionResult<BatchReviewResultDto>> SubmitBatchDecisions(
        [FromBody] BatchReviewDecisionDto request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Batch review request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ReviewerName))
        {
            return BadRequest(new { error = "Reviewer name is required for governance auditability (self-reported for demo mode)." });
        }

        if (request.ContentSubmissionIds == null || request.ContentSubmissionIds.Count == 0)
        {
            return BadRequest(new { error = "At least one submission ID must be provided." });
        }

        try
        {
            var result = await _reviewService.SubmitBatchDecisionsAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch review decisions.");
            return StatusCode(500, new { error = "Failed to process batch human review decisions." });
        }
    }
}
