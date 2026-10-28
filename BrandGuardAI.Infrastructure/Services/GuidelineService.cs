using System.Text.Json;
using System.Text.RegularExpressions;
using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Entities;
using BrandGuardAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Services;

public class GuidelineService : IGuidelineService
{
    private readonly IApplicationDbContext _context;
    private readonly IPdfTextExtractor _pdfExtractor;
    private readonly IDocumentChunker _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GuidelineService> _logger;

    public GuidelineService(
        IApplicationDbContext context,
        IPdfTextExtractor pdfExtractor,
        IDocumentChunker chunker,
        IEmbeddingService embeddingService,
        ILogger<GuidelineService> logger)
    {
        _context = context;
        _pdfExtractor = pdfExtractor;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<GuidelineDto> UploadGuidelineAsync(string title, string fileName, Stream fileStream, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only PDF guideline documents are supported.", nameof(fileName));
        }

        var guidelineId = Guid.NewGuid();
        var safeFileName = $"{guidelineId}_{Path.GetFileName(fileName)}";
        var storageDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "guidelines");
        Directory.CreateDirectory(storageDir);
        var targetPath = Path.Combine(storageDir, safeFileName);

        // Copy stream to file and memory
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        using (var fileOut = File.Create(targetPath))
        {
            await memoryStream.CopyToAsync(fileOut, cancellationToken);
        }
        memoryStream.Position = 0;

        var guideline = new BrandGuideline
        {
            Id = guidelineId,
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title,
            FileName = fileName,
            FilePath = targetPath,
            ExtractedText = string.Empty,
            Status = GuidelineStatus.Processing,
            UploadedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.BrandGuidelines.Add(guideline);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            // 1. Extract text page-by-page
            var pages = await _pdfExtractor.ExtractPagesAsync(memoryStream, cancellationToken);
            if (pages.Count == 0)
            {
                guideline.Status = GuidelineStatus.Failed;
                await _context.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("The uploaded PDF does not contain extractable text.");
            }

            var fullText = string.Join("\n\n--- Page Break ---\n\n", pages.Select(p => $"Page {p.PageNumber}:\n{p.Text}"));
            guideline.ExtractedText = fullText;

            // 2. Chunk document preserving page numbers and section headers
            var chunks = _chunker.ChunkPages(pages);

            // 3. Generate embeddings and store chunks
            var chunkEntities = new List<GuidelineChunk>();
            foreach (var chunk in chunks)
            {
                var vector = await _embeddingService.GenerateEmbeddingAsync(chunk.Text, cancellationToken);
                var vectorJson = JsonSerializer.Serialize(vector);

                chunkEntities.Add(new GuidelineChunk
                {
                    Id = Guid.NewGuid(),
                    BrandGuidelineId = guideline.Id,
                    ChunkIndex = chunk.ChunkIndex,
                    ChunkText = chunk.Text,
                    PageNumber = chunk.PageNumber,
                    SectionTitle = chunk.SectionTitle,
                    EmbeddingData = vectorJson,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.GuidelineChunks.AddRange(chunkEntities);

            // 4. Automatically extract dynamic deterministic rules from explicit guideline prohibitions
            ExtractDynamicRules(guideline.Id, fullText);

            guideline.Status = GuidelineStatus.Ready;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully indexed brand guideline '{Title}' ({ChunkCount} chunks)", guideline.Title, chunkEntities.Count);

            return new GuidelineDto(
                guideline.Id,
                guideline.Title,
                guideline.FileName,
                guideline.Status,
                guideline.UploadedAt,
                guideline.IsActive,
                chunkEntities.Count,
                fullText.Length
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index brand guideline {GuidelineId}", guideline.Id);
            guideline.Status = GuidelineStatus.Failed;
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private void ExtractDynamicRules(Guid guidelineId, string text)
    {
        // Search for explicit prohibitions like "must not contain X", "never use Y", "prohibited: Z"
        var matches = Regex.Matches(text, @"(?:must\s+not|prohibited|never\s+use|do\s+not\s+claim)\s+[""']?([^.\n"";]{3,50})[""']?", RegexOptions.IgnoreCase);
        foreach (Match m in matches.Take(3))
        {
            var phrase = m.Groups[1].Value.Trim();
            if (phrase.Length > 3 && !phrase.Contains("guarantee", StringComparison.OrdinalIgnoreCase))
            {
                var escaped = Regex.Escape(phrase);
                _context.ValidationRules.Add(new ValidationRule
                {
                    Id = Guid.NewGuid(),
                    RuleName = $"Guideline Policy: No {phrase}",
                    RuleType = RuleType.ProhibitedTerm,
                    RuleValue = $@"\b{escaped}\b",
                    Severity = RiskSeverity.High,
                    Description = $"Directly mandated by uploaded brand guideline policy: '{m.Value.Trim()}'.",
                    SourceGuidelineId = guidelineId,
                    IsActive = true
                });
            }
        }
    }

    public async Task<List<GuidelineDto>> GetAllGuidelinesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.BrandGuidelines
            .OrderByDescending(g => g.UploadedAt)
            .Select(g => new GuidelineDto(
                g.Id,
                g.Title,
                g.FileName,
                g.Status,
                g.UploadedAt,
                g.IsActive,
                g.Chunks.Count,
                g.ExtractedText.Length
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<GuidelineDetailDto?> GetGuidelineByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var guideline = await _context.BrandGuidelines
            .Include(g => g.Chunks)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (guideline == null)
        {
            return null;
        }

        var chunkDtos = guideline.Chunks
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new GuidelineChunkDto(
                c.Id,
                c.BrandGuidelineId,
                c.ChunkIndex,
                c.ChunkText,
                c.PageNumber,
                c.SectionTitle,
                c.CreatedAt
            ))
            .ToList();

        return new GuidelineDetailDto(
            guideline.Id,
            guideline.Title,
            guideline.FileName,
            guideline.FilePath,
            guideline.ExtractedText,
            guideline.Status,
            guideline.UploadedAt,
            guideline.IsActive,
            chunkDtos
        );
    }

    public async Task<List<GuidelineChunkDto>> GetGuidelineChunksAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GuidelineChunks
            .Where(c => c.BrandGuidelineId == id)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new GuidelineChunkDto(
                c.Id,
                c.BrandGuidelineId,
                c.ChunkIndex,
                c.ChunkText,
                c.PageNumber,
                c.SectionTitle,
                c.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteGuidelineAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var guideline = await _context.BrandGuidelines
            .Include(g => g.Chunks)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (guideline == null)
        {
            return false;
        }

        _context.BrandGuidelines.Remove(guideline);
        await _context.SaveChangesAsync(cancellationToken);

        if (File.Exists(guideline.FilePath))
        {
            try { File.Delete(guideline.FilePath); } catch { /* ignore */ }
        }

        return true;
    }
}
