using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Application.Common.Models;

namespace BrandGuardAI.Application.Common.Interfaces;

public interface IGuidelineService
{
    Task<GuidelineDto> UploadGuidelineAsync(string title, string fileName, Stream fileStream, CancellationToken cancellationToken = default);
    Task<List<GuidelineDto>> GetAllGuidelinesAsync(CancellationToken cancellationToken = default);
    Task<GuidelineDetailDto?> GetGuidelineByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<GuidelineChunkDto>> GetGuidelineChunksAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteGuidelineAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IContentSubmissionService
{
    Task<ContentSubmissionDto> CreateSubmissionAsync(CreateContentSubmissionDto dto, CancellationToken cancellationToken = default);
    Task<List<ContentSubmissionDto>> GetAllSubmissionsAsync(CancellationToken cancellationToken = default);
    Task<ContentSubmissionDetailDto?> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContentSubmissionDto> UpdateSubmissionAsync(Guid id, UpdateContentSubmissionDto dto, CancellationToken cancellationToken = default);
    Task<ValidationResultDto?> GetLatestValidationResultAsync(Guid submissionId, CancellationToken cancellationToken = default);
}

public interface IReviewService
{
    Task<ReviewDecisionDto> SubmitDecisionAsync(SubmitReviewDecisionDto dto, CancellationToken cancellationToken = default);
    Task<List<ReviewHistoryItemDto>> GetReviewHistoryAsync(CancellationToken cancellationToken = default);
    Task<ReviewDecisionDto?> GetDecisionBySubmissionIdAsync(Guid submissionId, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}
