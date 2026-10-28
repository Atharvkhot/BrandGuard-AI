using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Domain.Entities;

public class ValidationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleName { get; set; } = string.Empty;
    public RuleType RuleType { get; set; } = RuleType.ProhibitedTerm;
    public string RuleValue { get; set; } = string.Empty;
    public RiskSeverity Severity { get; set; } = RiskSeverity.High;
    public string Description { get; set; } = string.Empty;
    public Guid? SourceGuidelineId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public BrandGuideline? SourceGuideline { get; set; }
}
