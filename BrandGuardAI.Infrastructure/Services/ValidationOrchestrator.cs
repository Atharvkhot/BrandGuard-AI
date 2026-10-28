using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Services;

public class ValidationOrchestrator : IValidationOrchestrator
{
    private readonly IApplicationDbContext _context;
    private readonly IDeterministicRuleEngine _ruleEngine;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly ILlmValidationService _llmService;
    private readonly ILogger<ValidationOrchestrator> _logger;

    public ValidationOrchestrator(
        IApplicationDbContext context,
        IDeterministicRuleEngine ruleEngine,
        ISemanticSearchService semanticSearchService,
        ILlmValidationService llmService,
        ILogger<ValidationOrchestrator> logger)
    {
        _context = context;
        _ruleEngine = ruleEngine;
        _semanticSearchService = semanticSearchService;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ValidationResultDto> RunValidationAsync(Guid contentSubmissionId, CancellationToken cancellationToken = default)
    {
        var submission = await _context.ContentSubmissions
            .FirstOrDefaultAsync(s => s.Id == contentSubmissionId, cancellationToken);

        if (submission == null)
        {
            throw new KeyNotFoundException($"Content submission with ID {contentSubmissionId} not found.");
        }

        submission.Status = ContentSubmissionStatus.Validating;
        submission.UpdatedAt = DateTime.UtcNow;

        var validationRun = new ValidationRun
        {
            Id = Guid.NewGuid(),
            ContentSubmissionId = submission.Id,
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            OverallRiskLevel = RiskSeverity.Low,
            TotalFindings = 0
        };

        _context.ValidationRuns.Add(validationRun);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            // 1. Run deterministic rule validation
            var deterministicResults = await _ruleEngine.EvaluateContentAsync(submission.ContentText, cancellationToken);

            // 2. Retrieve relevant guideline chunks via semantic similarity
            var retrievedChunks = await _semanticSearchService.SearchSimilarChunksAsync(
                submission.ContentText,
                topK: 5,
                minSimilarity: 0.15f,
                cancellationToken: cancellationToken);

            // 3. Run LLM validation with context and deterministic flags
            var llmRequest = new LlmValidationRequest(
                submission.Title,
                submission.ContentType,
                submission.ContentText,
                retrievedChunks,
                deterministicResults);

            var llmResponse = await _llmService.ValidateContentAsync(llmRequest, cancellationToken);

            // 4. Merge findings & remove duplicates
            var mergedFindings = new List<ValidationFinding>();
            var coveredTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Process Deterministic Findings first (highest reliability)
            foreach (var det in deterministicResults)
            {
                // Match with closest retrieved chunk if available
                var matchingChunk = retrievedChunks.FirstOrDefault(c =>
                    c.ChunkText.Contains(det.MatchedText, StringComparison.OrdinalIgnoreCase) ||
                    (det.SourceGuidelineId.HasValue && c.GuidelineId == det.SourceGuidelineId.Value));

                var finding = new ValidationFinding
                {
                    Id = Guid.NewGuid(),
                    ValidationRunId = validationRun.Id,
                    GuidelineChunkId = matchingChunk?.ChunkId ?? retrievedChunks.FirstOrDefault()?.ChunkId,
                    IssueType = det.RuleName,
                    Severity = det.Severity,
                    OriginalText = det.MatchedText,
                    Explanation = det.Explanation,
                    SuggestedFix = det.SuggestedFix,
                    EvidenceText = matchingChunk != null ? matchingChunk.ChunkText : det.EvidenceText,
                    CreatedAt = DateTime.UtcNow
                };

                mergedFindings.Add(finding);
                coveredTexts.Add(det.MatchedText);
            }

            // Process LLM Findings (augmenting with tone, nuance, contextual checks)
            foreach (var llmFinding in llmResponse.Findings)
            {
                // Deduplicate if already covered by deterministic finding with same text
                if (!string.IsNullOrWhiteSpace(llmFinding.AffectedText) &&
                    coveredTexts.Any(t => t.Contains(llmFinding.AffectedText, StringComparison.OrdinalIgnoreCase) ||
                                          llmFinding.AffectedText.Contains(t, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                Guid? verifiedChunkId = null;
                string evidence = llmFinding.EvidenceQuote ?? string.Empty;

                if (llmFinding.CitedChunkId.HasValue &&
                    retrievedChunks.Any(c => c.ChunkId == llmFinding.CitedChunkId.Value))
                {
                    verifiedChunkId = llmFinding.CitedChunkId.Value;
                    var citedChunk = retrievedChunks.First(c => c.ChunkId == llmFinding.CitedChunkId.Value);
                    if (string.IsNullOrWhiteSpace(evidence))
                    {
                        evidence = citedChunk.ChunkText;
                    }
                }
                else if (retrievedChunks.Count > 0)
                {
                    var fallbackChunk = retrievedChunks.First();
                    verifiedChunkId = fallbackChunk.ChunkId;
                    evidence = string.IsNullOrWhiteSpace(evidence) ? fallbackChunk.ChunkText : evidence;
                }

                var finding = new ValidationFinding
                {
                    Id = Guid.NewGuid(),
                    ValidationRunId = validationRun.Id,
                    GuidelineChunkId = verifiedChunkId,
                    IssueType = llmFinding.IssueCategory,
                    Severity = llmFinding.Severity,
                    OriginalText = string.IsNullOrWhiteSpace(llmFinding.AffectedText) ? submission.Title : llmFinding.AffectedText,
                    Explanation = llmFinding.Explanation,
                    SuggestedFix = llmFinding.SuggestedCorrection,
                    EvidenceText = evidence,
                    CreatedAt = DateTime.UtcNow
                };

                mergedFindings.Add(finding);
                if (!string.IsNullOrWhiteSpace(llmFinding.AffectedText))
                {
                    coveredTexts.Add(llmFinding.AffectedText);
                }
            }

            // 5. Calculate overall risk
            var overallRisk = RiskSeverity.Low;
            if (mergedFindings.Any(f => f.Severity == RiskSeverity.High))
            {
                overallRisk = RiskSeverity.High;
            }
            else if (mergedFindings.Any(f => f.Severity == RiskSeverity.Medium))
            {
                overallRisk = RiskSeverity.Medium;
            }

            // Update ValidationRun
            validationRun.Status = "Completed";
            validationRun.CompletedAt = DateTime.UtcNow;
            validationRun.OverallRiskLevel = overallRisk;
            validationRun.TotalFindings = mergedFindings.Count;

            _context.ValidationFindings.AddRange(mergedFindings);

            // Update Submission Status
            submission.Status = ContentSubmissionStatus.PendingReview;
            submission.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Build Result DTO
            var highCount = mergedFindings.Count(f => f.Severity == RiskSeverity.High);
            var medCount = mergedFindings.Count(f => f.Severity == RiskSeverity.Medium);
            var lowCount = mergedFindings.Count(f => f.Severity == RiskSeverity.Low);

            var chunkDict = retrievedChunks.ToDictionary(c => c.ChunkId, c => c);

            var findingDtos = mergedFindings.Select(f =>
            {
                ChunkSearchResult? chunk = null;
                if (f.GuidelineChunkId.HasValue)
                {
                    chunkDict.TryGetValue(f.GuidelineChunkId.Value, out chunk);
                }

                return new FindingDto(
                    f.Id,
                    f.ValidationRunId,
                    f.GuidelineChunkId,
                    f.IssueType,
                    f.Severity,
                    f.OriginalText,
                    f.Explanation,
                    f.SuggestedFix,
                    f.EvidenceText,
                    chunk?.GuidelineTitle ?? "Corporate Brand Policy",
                    chunk?.PageNumber,
                    chunk?.SectionTitle,
                    f.CreatedAt
                );
            }).ToList();

            _logger.LogInformation("Completed validation run {RunId} with {Total} findings (High: {High}, Med: {Med}, Low: {Low})",
                validationRun.Id, mergedFindings.Count, highCount, medCount, lowCount);

            return new ValidationResultDto(
                validationRun.Id,
                submission.Id,
                submission.Title,
                submission.ContentType,
                submission.ContentText,
                validationRun.Status,
                overallRisk,
                mergedFindings.Count,
                highCount,
                medCount,
                lowCount,
                validationRun.StartedAt,
                validationRun.CompletedAt,
                null,
                findingDtos
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for submission {SubmissionId}", contentSubmissionId);
            validationRun.Status = "Failed";
            validationRun.ErrorMessage = ex.Message;
            validationRun.CompletedAt = DateTime.UtcNow;
            submission.Status = ContentSubmissionStatus.Draft;
            await _context.SaveChangesAsync(cancellationToken);

            throw;
        }
    }
}
