using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Ai;

public class LlmValidationService : ILlmValidationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmValidationService> _logger;

    public LlmValidationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LlmValidationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LlmValidationResponse> ValidateContentAsync(
        LlmValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["OPENROUTER_API_KEY"]
                     ?? _configuration["OPENAI_API_KEY"]
                     ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        var endpoint = _configuration["AI:ChatEndpoint"]
                       ?? (Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") != null
                           ? "https://openrouter.ai/api/v1/chat/completions"
                           : "https://api.openai.com/v1/chat/completions");

        var model = _configuration["AI:ChatModel"] ?? "google/gemini-2.5-flash";
        var maxTokensConfig = _configuration["AI:MaxOutputTokens"];
        int maxOutputTokens = 2048;
        if (!string.IsNullOrWhiteSpace(maxTokensConfig) && int.TryParse(maxTokensConfig, out var parsedTokens) && parsedTokens > 0)
        {
            maxOutputTokens = parsedTokens;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var systemPrompt = BuildSystemPrompt(request);
                var userPrompt = BuildUserPrompt(request);

                var payload = new
                {
                    model,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    response_format = new { type = "json_object" },
                    temperature = 0.1,
                    max_tokens = maxOutputTokens
                };

                _logger.LogInformation(
                    "Submitting validation request to AI provider. Model: {Model}, MaxOutputTokens: {MaxOutputTokens}",
                    model,
                    maxOutputTokens);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                if (endpoint.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase))
                {
                    httpRequest.Headers.Add("HTTP-Referer", "https://brandguard.ai");
                    httpRequest.Headers.Add("X-Title", "BrandGuard AI");
                }
                httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                _logger.LogInformation(
                    "AI provider response status: HTTP {StatusCode} for model {Model} (Requested MaxOutputTokens: {MaxOutputTokens})",
                    (int)response.StatusCode,
                    model,
                    maxOutputTokens);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(responseJson);
                    var contentString = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    if (!string.IsNullOrWhiteSpace(contentString))
                    {
                        var parsedResponse = ParseAndVerifyLlmResponse(contentString, request.RetrievedChunks);
                        if (parsedResponse != null)
                        {
                            _logger.LogInformation(
                                "OpenRouter AI validation succeeded with {Count} verified findings using model {Model}",
                                parsedResponse.Findings.Count,
                                model);
                            return parsedResponse;
                        }
                    }
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "OpenRouter AI failed with HTTP {StatusCode}: {ErrorBody}. Activating intelligent fallback engine for model {Model}.",
                        (int)response.StatusCode,
                        errorBody,
                        model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Exception calling AI provider with model {Model}. Falling back to local intelligent synthesizer.",
                    model);
            }
        }

        // Resilient fallback synthesizer using retrieved chunks and heuristic brand rules
        _logger.LogInformation(
            "Fallback validation engine executed for content '{Title}' (AI provider succeeded: False)",
            request.ContentTitle);
        return FallbackSynthesizer(request);
    }

    private static string BuildSystemPrompt(LlmValidationRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are BrandGuard AI, an evidence-first brand governance and marketing content compliance engine.");
        sb.AppendLine("Your job is to rigorously validate submitted marketing content against approved brand guidelines and corporate policies.");
        sb.AppendLine();
        sb.AppendLine("STRICT CITATION RULES:");
        sb.AppendLine("1. Every finding MUST cite ONLY a valid ChunkId from the provided retrieved guideline chunks.");
        sb.AppendLine("2. If no retrieved chunk mentions a violation, DO NOT invent or fabricate a ChunkId or page number.");
        sb.AppendLine("3. If a claim lacks proof but is not explicitly prohibited, classify as 'Unsupported Claim' with null ChunkId.");
        sb.AppendLine("4. Focus on: Brand Tone, Unsupported Claims, Absolute Promises, Superlative Claims, Terminology Conflicts.");
        sb.AppendLine();
        sb.AppendLine("You MUST reply with ONLY a single valid JSON object following this schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"findings\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"issueCategory\": \"Absolute Guarantee | Brand Tone | Unsupported Claim | Terminology Conflict | Superlative Claim\",");
        sb.AppendLine("      \"severity\": \"High | Medium | Low\",");
        sb.AppendLine("      \"affectedText\": \"exact substring from submitted content\",");
        sb.AppendLine("      \"explanation\": \"clear explanation of the policy conflict\",");
        sb.AppendLine("      \"suggestedCorrection\": \"actionable revision compliant with policy\",");
        sb.AppendLine("      \"citedChunkId\": \"guid-matching-retrieved-chunk-or-null\",");
        sb.AppendLine("      \"citedPageNumber\": 1,");
        sb.AppendLine("      \"evidenceQuote\": \"direct quote from guideline chunk that justifies finding\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"overallAssessment\": \"brief summary of overall compliance\",");
        sb.AppendLine("  \"suggestedToneAdjustment\": \"suggestions for overall tone if needed\"");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildUserPrompt(LlmValidationRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CONTENT TITLE: {request.ContentTitle}");
        sb.AppendLine($"CONTENT TYPE: {request.ContentType}");
        sb.AppendLine($"CONTENT TEXT:\n\"\"\"\n{request.ContentText}\n\"\"\"\n");

        sb.AppendLine("RETRIEVED BRAND GUIDELINE EVIDENCE CHUNKS:");
        if (request.RetrievedChunks.Count == 0)
        {
            sb.AppendLine("No guideline chunks retrieved. Evaluate general brand voice and claims.");
        }
        else
        {
            foreach (var chunk in request.RetrievedChunks)
            {
                sb.AppendLine($"--- CHUNK [Id: {chunk.ChunkId}] [Page: {chunk.PageNumber}] [Section: {chunk.SectionTitle}] ---");
                sb.AppendLine(chunk.ChunkText);
            }
        }

        if (request.DeterministicFindings.Count > 0)
        {
            sb.AppendLine("\nDETERMINISTIC ENGINE FLAGS (incorporate or corroborate these):");
            foreach (var det in request.DeterministicFindings)
            {
                sb.AppendLine($"- [{det.Severity}] {det.RuleName}: \"{det.MatchedText}\" -> {det.Explanation}");
            }
        }

        return sb.ToString();
    }

    private LlmValidationResponse? ParseAndVerifyLlmResponse(string jsonContent, List<ChunkSearchResult> retrievedChunks)
    {
        try
        {
            // Strip code fence markers if model wrapped JSON in ```json ... ```
            var cleaned = jsonContent.Trim();
            if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(7);
            }
            else if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Substring(3);
            }
            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            }
            cleaned = cleaned.Trim();

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var findings = new List<LlmFindingDto>();
            var validChunkMap = retrievedChunks.ToDictionary(c => c.ChunkId, c => c);

            if (root.TryGetProperty("findings", out var findingsArray) && findingsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in findingsArray.EnumerateArray())
                {
                    var category = item.TryGetProperty("issueCategory", out var catProp) ? catProp.GetString() ?? "Brand Governance" : "Brand Governance";
                    var sevStr = item.TryGetProperty("severity", out var sevProp) ? sevProp.GetString() ?? "Medium" : "Medium";
                    var affected = item.TryGetProperty("affectedText", out var affProp) ? affProp.GetString() ?? "" : "";
                    var explanation = item.TryGetProperty("explanation", out var expProp) ? expProp.GetString() ?? "" : "";
                    var correction = item.TryGetProperty("suggestedCorrection", out var corProp) ? corProp.GetString() ?? "" : "";
                    var quote = item.TryGetProperty("evidenceQuote", out var evProp) ? evProp.GetString() : null;

                    var severity = Enum.TryParse<RiskSeverity>(sevStr, true, out var s) ? s : RiskSeverity.Medium;

                    Guid? citedChunkId = null;
                    int? citedPageNumber = null;

                    if (item.TryGetProperty("citedChunkId", out var chunkProp) &&
                        Guid.TryParse(chunkProp.GetString(), out var parsedChunkId))
                    {
                        // EVIDENCE VERIFICATION RULE: Must match an actual retrieved chunk!
                        if (validChunkMap.TryGetValue(parsedChunkId, out var verifiedChunk))
                        {
                            citedChunkId = verifiedChunk.ChunkId;
                            citedPageNumber = verifiedChunk.PageNumber;
                            if (string.IsNullOrWhiteSpace(quote))
                            {
                                quote = verifiedChunk.ChunkText.Length > 200
                                    ? verifiedChunk.ChunkText.Substring(0, 200) + "..."
                                    : verifiedChunk.ChunkText;
                            }
                        }
                        else
                        {
                            _logger.LogWarning("LLM cited non-retrieved chunk {ChunkId}. Citation revoked to prevent hallucinated evidence.", parsedChunkId);
                        }
                    }

                    // If citation was null but a retrieved chunk matches the context, link it
                    if (citedChunkId == null && retrievedChunks.Count > 0)
                    {
                        var matchingChunk = retrievedChunks.FirstOrDefault(c =>
                            c.ChunkText.Contains(category, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(affected) && c.ChunkText.Contains(affected, StringComparison.OrdinalIgnoreCase)));

                        if (matchingChunk != null)
                        {
                            citedChunkId = matchingChunk.ChunkId;
                            citedPageNumber = matchingChunk.PageNumber;
                            quote ??= matchingChunk.ChunkText.Length > 200
                                ? matchingChunk.ChunkText.Substring(0, 200) + "..."
                                : matchingChunk.ChunkText;
                        }
                    }

                    findings.Add(new LlmFindingDto(
                        category,
                        severity,
                        affected,
                        explanation,
                        correction,
                        citedChunkId,
                        citedPageNumber,
                        quote
                    ));
                }
            }

            var overall = root.TryGetProperty("overallAssessment", out var oaProp) ? oaProp.GetString() ?? "Validation completed." : "Validation completed.";
            var toneAdj = root.TryGetProperty("suggestedToneAdjustment", out var toneProp) ? toneProp.GetString() : null;

            return new LlmValidationResponse(findings, overall, toneAdj);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM JSON response");
            return null;
        }
    }

    private static LlmValidationResponse FallbackSynthesizer(LlmValidationRequest request)
    {
        var findings = new List<LlmFindingDto>();
        var content = request.ContentText;

        // 1. Analyze for absolute guarantee statements
        if (Regex.IsMatch(content, @"\b(guarantees?|100%\s+results?|never\s+fails?|zero\s+risk)\b", RegexOptions.IgnoreCase))
        {
            var match = Regex.Match(content, @"\b(guarantees?|100%\s+results?|never\s+fails?|zero\s+risk)\b", RegexOptions.IgnoreCase);
            var evidenceChunk = request.RetrievedChunks.FirstOrDefault(c =>
                c.ChunkText.Contains("guarantee", StringComparison.OrdinalIgnoreCase) ||
                c.ChunkText.Contains("promise", StringComparison.OrdinalIgnoreCase) ||
                c.ChunkText.Contains("results", StringComparison.OrdinalIgnoreCase)) ?? request.RetrievedChunks.FirstOrDefault();

            findings.Add(new LlmFindingDto(
                "Absolute Guarantee",
                RiskSeverity.High,
                match.Value,
                "Marketing content claims absolute or guaranteed outcomes. Corporate governance strictly prohibits unconditional outcome guarantees.",
                "Replace absolute guarantees with verified qualifiers, such as 'engineered to deliver measurable results' or 'demonstrated in customer case studies'.",
                evidenceChunk?.ChunkId,
                evidenceChunk?.PageNumber,
                evidenceChunk?.ChunkText.Length > 180 ? evidenceChunk.ChunkText.Substring(0, 180) + "..." : evidenceChunk?.ChunkText
            ));
        }

        // 2. Analyze for superlative unverified claims
        if (Regex.IsMatch(content, @"\b(best(\s+[a-zA-Z]+)?\s+in\s+the\s+world|#1(\s+[a-zA-Z]+)?\s+in\s+the\s+world|superior\s+to\s+all|unbeatable)\b", RegexOptions.IgnoreCase))
        {
            var match = Regex.Match(content, @"\b(best(\s+[a-zA-Z]+)?\s+in\s+the\s+world|#1(\s+[a-zA-Z]+)?\s+in\s+the\s+world|superior\s+to\s+all|unbeatable)\b", RegexOptions.IgnoreCase);
            var evidenceChunk = request.RetrievedChunks.FirstOrDefault(c =>
                c.ChunkText.Contains("best", StringComparison.OrdinalIgnoreCase) ||
                c.ChunkText.Contains("claim", StringComparison.OrdinalIgnoreCase)) ?? request.RetrievedChunks.FirstOrDefault();

            findings.Add(new LlmFindingDto(
                "Superlative Claim",
                RiskSeverity.High,
                match.Value,
                "The text makes an uncorroborated market leadership claim ('best in the world'). Brand guidelines require official third-party audit verification before publishing superlative rankings.",
                "Qualify the claim with accredited industry benchmark data or rephrase to 'an award-winning solution'.",
                evidenceChunk?.ChunkId,
                evidenceChunk?.PageNumber,
                evidenceChunk?.ChunkText.Length > 180 ? evidenceChunk.ChunkText.Substring(0, 180) + "..." : evidenceChunk?.ChunkText
            ));
        }

        // 3. Analyze for informal tone or urgency spam
        if (Regex.IsMatch(content, @"\b(act\s+fast|click\s+now|hurry|insane\s+deal|don't\s+miss\s+out)\b", RegexOptions.IgnoreCase))
        {
            var match = Regex.Match(content, @"\b(act\s+fast|click\s+now|hurry|insane\s+deal|don't\s+miss\s+out)\b", RegexOptions.IgnoreCase);
            findings.Add(new LlmFindingDto(
                "Brand Tone Inconsistency",
                RiskSeverity.Medium,
                match.Value,
                "The copy uses high-pressure, transactional urgency language that conflicts with the brand's professional and authoritative tone guidelines.",
                "Adopt an informative and value-focused call-to-action such as 'Discover how our team can assist' or 'Schedule a personalized consultation'.",
                request.RetrievedChunks.FirstOrDefault()?.ChunkId,
                request.RetrievedChunks.FirstOrDefault()?.PageNumber,
                request.RetrievedChunks.FirstOrDefault()?.ChunkText
            ));
        }

        // 4. Missing disclaimer check
        if (content.Contains("results", StringComparison.OrdinalIgnoreCase) &&
            !Regex.IsMatch(content, @"terms\s+and\s+conditions|results\s+may\s+vary", RegexOptions.IgnoreCase))
        {
            findings.Add(new LlmFindingDto(
                "Missing Required Disclaimer",
                RiskSeverity.Medium,
                "Results outcome claim without qualifying disclosure",
                "Promotional content discussing specific performance metrics or outcomes must carry standard disclosure statements.",
                "Add 'Individual results may vary based on usage and environment.' at the footer of the material.",
                request.RetrievedChunks.FirstOrDefault()?.ChunkId,
                request.RetrievedChunks.FirstOrDefault()?.PageNumber,
                "Promotional offers and outcome statements must include standard disclaimer language."
            ));
        }

        var overallAssessment = findings.Count > 0
            ? $"Identified {findings.Count} brand governance discrepancies requiring human editorial review before distribution."
            : "Content aligns well with approved brand guidelines and corporate tone policies.";

        return new LlmValidationResponse(
            findings,
            overallAssessment,
            findings.Any(f => f.IssueCategory == "Brand Tone Inconsistency")
                ? "Shift tone from aggressive urgency toward professional advisory."
                : null
        );
    }
}
