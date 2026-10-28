using System.Text.RegularExpressions;
using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Rules;

public class DeterministicRuleEngine : IDeterministicRuleEngine
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DeterministicRuleEngine> _logger;

    public DeterministicRuleEngine(
        IApplicationDbContext context,
        ILogger<DeterministicRuleEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<RuleEvaluationResult>> EvaluateContentAsync(string content, CancellationToken cancellationToken = default)
    {
        var findings = new List<RuleEvaluationResult>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return findings;
        }

        var activeRules = await _context.ValidationRules
            .Where(r => r.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var rule in activeRules)
        {
            try
            {
                if (rule.RuleType == RuleType.RequiredDisclaimer)
                {
                    // Check if content implies claims requiring disclaimers but lacks it
                    var claimIndicators = new[] { "offer", "discount", "save", "results", "earn", "grow", "bonus" };
                    bool hasClaim = claimIndicators.Any(c => content.Contains(c, StringComparison.OrdinalIgnoreCase));
                    bool hasDisclaimer = Regex.IsMatch(content, rule.RuleValue, RegexOptions.IgnoreCase);

                    if (hasClaim && !hasDisclaimer)
                    {
                        findings.Add(new RuleEvaluationResult(
                            rule.Id,
                            rule.RuleName,
                            rule.RuleType,
                            rule.Severity,
                            "Missing required promotional disclaimer",
                            $"{rule.Description} Content contains promotional outcome statements without stating required terms.",
                            "Append standard brand disclaimer (e.g., 'Terms & conditions apply. Results may vary.')",
                            rule.Description,
                            rule.SourceGuidelineId
                        ));
                    }
                }
                else
                {
                    // Match pattern against content
                    var matches = Regex.Matches(content, rule.RuleValue, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        var matchedText = match.Value;

                        string explanation = rule.RuleType switch
                        {
                            RuleType.AbsoluteGuarantee => $"The phrase '{matchedText}' represents an absolute guarantee. Brand policy strictly prohibits promise of 100% or guaranteed outcomes.",
                            RuleType.RestrictedPhrase => $"The phrase '{matchedText}' makes an unsubstantiated superlative claim. Brand policy mandates external accreditation before making #1 or superior claims.",
                            RuleType.ProhibitedTerm => $"The term '{matchedText}' is on the prohibited terminology register for this brand category.",
                            RuleType.ApprovedTerminology => $"'{matchedText}' conflicts with the approved terminology guidelines.",
                            _ => rule.Description
                        };

                        string suggestedFix = rule.RuleType switch
                        {
                            RuleType.AbsoluteGuarantee => "Replace absolute promises with measurable qualifications, e.g., 'designed to help you achieve measurable results' or 'proven in customer trials'.",
                            RuleType.RestrictedPhrase => "Cite specific third-party test data or rephrase to 'an industry-recognized solution' rather than unverified superlatives.",
                            RuleType.ProhibitedTerm => $"Remove '{matchedText}' and substitute with approved standard language.",
                            _ => "Adjust wording to comply with official brand policy standards."
                        };

                        findings.Add(new RuleEvaluationResult(
                            rule.Id,
                            rule.RuleName,
                            rule.RuleType,
                            rule.Severity,
                            matchedText,
                            explanation,
                            suggestedFix,
                            rule.Description,
                            rule.SourceGuidelineId
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed evaluating rule {RuleName} with pattern {Pattern}", rule.RuleName, rule.RuleValue);
            }
        }

        _logger.LogInformation("Deterministic rule engine identified {FindingCount} rule violations", findings.Count);
        return findings;
    }
}
