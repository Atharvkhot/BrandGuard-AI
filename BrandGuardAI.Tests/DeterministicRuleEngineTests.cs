using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using BrandGuardAI.Infrastructure.Data;
using BrandGuardAI.Infrastructure.Rules;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrandGuardAI.Tests;

public class DeterministicRuleEngineTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task EvaluateContentAsync_ShouldDetectAbsoluteGuaranteeViolation()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var engine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var input = "Our product guarantees 100% results and zero risk.";

        // Act
        var findings = await engine.EvaluateContentAsync(input);

        // Assert
        findings.Should().NotBeEmpty();
        var guaranteeFinding = findings.FirstOrDefault(f => f.RuleType == RuleType.AbsoluteGuarantee);
        guaranteeFinding.Should().NotBeNull();
        guaranteeFinding!.Severity.Should().Be(RiskSeverity.High);
        guaranteeFinding.Explanation.Should().Contain("guarantee");
    }

    [Fact]
    public async Task EvaluateContentAsync_ShouldDetectSuperlativeClaim()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var engine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var input = "We provide the best in the world enterprise solution.";

        // Act
        var findings = await engine.EvaluateContentAsync(input);

        // Assert
        var superlative = findings.FirstOrDefault(f => f.RuleType == RuleType.RestrictedPhrase);
        superlative.Should().NotBeNull();
        superlative!.MatchedText.Should().Be("best in the world");
        superlative.Severity.Should().Be(RiskSeverity.High);
    }

    [Fact]
    public async Task EvaluateContentAsync_ShouldDetectMissingRequiredDisclaimer()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var engine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        // Has promotional claim indicators like "results", but missing "terms and conditions apply" or "results may vary"
        var input = "Users report high growth and measurable results when adopting the platform.";

        // Act
        var findings = await engine.EvaluateContentAsync(input);

        // Assert
        var disclaimerFinding = findings.FirstOrDefault(f => f.RuleType == RuleType.RequiredDisclaimer);
        disclaimerFinding.Should().NotBeNull();
        disclaimerFinding!.Explanation.Should().Contain("disclaimer");
    }

    [Fact]
    public async Task EvaluateContentAsync_ShouldDetectSuperlativeWithInterveningNoun()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var engine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var input = "Our product guarantees 100% results and is the best solution in the world.";

        // Act
        var findings = await engine.EvaluateContentAsync(input);

        // Assert
        var superlative = findings.FirstOrDefault(f => f.RuleType == RuleType.RestrictedPhrase);
        superlative.Should().NotBeNull();
        superlative!.MatchedText.Should().Be("best solution in the world");
        superlative.Severity.Should().Be(RiskSeverity.High);
    }

    [Fact]
    public async Task EvaluateContentAsync_ShouldDetectProhibitedMedicalClaims()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var engine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var input = "Discover our clinically proven cure and miracle cure for chronic pain.";

        // Act
        var findings = await engine.EvaluateContentAsync(input);

        // Assert
        var medical = findings.Where(f => f.RuleType == RuleType.ProhibitedTerm).ToList();
        medical.Should().NotBeEmpty();
        medical.Any(f => f.MatchedText.Contains("cure")).Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateContentAsync_CompliantContent_ShouldReturnZeroViolations()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var engine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var input = "Our collaboration tool is designed to assist marketing teams in improving campaign organization.";

        // Act
        var findings = await engine.EvaluateContentAsync(input);

        // Assert
        findings.Should().BeEmpty("compliant text without claims or guarantees must not trigger false positives");
    }

    [Fact]
    public async Task EvaluateContentAsync_WhenDisclaimerPresent_ShouldNotTriggerMissingDisclaimer()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var engine = new DeterministicRuleEngine(context, NullLogger<DeterministicRuleEngine>.Instance);
        var input = "Customers achieve measurable results using our software. Terms and conditions apply. Results may vary.";

        // Act
        var findings = await engine.EvaluateContentAsync(input);

        // Assert
        var disclaimerFinding = findings.FirstOrDefault(f => f.RuleType == RuleType.RequiredDisclaimer);
        disclaimerFinding.Should().BeNull("disclaimer is explicitly present in the copy");
    }
}
