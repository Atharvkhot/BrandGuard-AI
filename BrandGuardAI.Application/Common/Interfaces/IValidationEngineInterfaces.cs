using BrandGuardAI.Application.Common.Models;

namespace BrandGuardAI.Application.Common.Interfaces;

public interface IDeterministicRuleEngine
{
    Task<List<RuleEvaluationResult>> EvaluateContentAsync(string content, CancellationToken cancellationToken = default);
}

public interface ILlmValidationService
{
    Task<LlmValidationResponse> ValidateContentAsync(LlmValidationRequest request, CancellationToken cancellationToken = default);
}

public interface IValidationOrchestrator
{
    Task<ValidationResultDto> RunValidationAsync(Guid contentSubmissionId, CancellationToken cancellationToken = default);
}
