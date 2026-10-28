using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Application.DTOs;

public record GuidelineDto(
    Guid Id,
    string Title,
    string FileName,
    GuidelineStatus Status,
    DateTime UploadedAt,
    bool IsActive,
    int ChunkCount,
    int TextLength
);

public record GuidelineDetailDto(
    Guid Id,
    string Title,
    string FileName,
    string FilePath,
    string ExtractedText,
    GuidelineStatus Status,
    DateTime UploadedAt,
    bool IsActive,
    List<GuidelineChunkDto> Chunks
);

public record GuidelineChunkDto(
    Guid Id,
    Guid BrandGuidelineId,
    int ChunkIndex,
    string ChunkText,
    int PageNumber,
    string SectionTitle,
    DateTime CreatedAt
);
