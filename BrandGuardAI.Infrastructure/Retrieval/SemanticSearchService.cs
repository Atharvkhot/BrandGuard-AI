using System.Text.Json;
using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Retrieval;

public class SemanticSearchService : ISemanticSearchService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        IApplicationDbContext context,
        IEmbeddingService embeddingService,
        ILogger<SemanticSearchService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<List<ChunkSearchResult>> SearchSimilarChunksAsync(
        string query,
        int topK = 5,
        float minSimilarity = 0.20f,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<ChunkSearchResult>();
        }

        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        var activeChunks = await _context.GuidelineChunks
            .Include(c => c.BrandGuideline)
            .Where(c => c.BrandGuideline != null && c.BrandGuideline.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (activeChunks.Count == 0)
        {
            _logger.LogWarning("No active guideline chunks available for semantic retrieval.");
            return new List<ChunkSearchResult>();
        }

        var results = new List<ChunkSearchResult>();

        foreach (var chunk in activeChunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.EmbeddingData))
            {
                continue;
            }

            try
            {
                var chunkVector = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingData);
                if (chunkVector == null || chunkVector.Length != queryVector.Length)
                {
                    continue;
                }

                var similarity = CalculateCosineSimilarity(queryVector, chunkVector);

                if (similarity >= minSimilarity)
                {
                    results.Add(new ChunkSearchResult(
                        chunk.Id,
                        chunk.BrandGuidelineId,
                        chunk.BrandGuideline?.Title ?? "Brand Guideline",
                        chunk.PageNumber,
                        chunk.SectionTitle,
                        chunk.ChunkText,
                        similarity
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize embedding for chunk {ChunkId}", chunk.Id);
            }
        }

        var sorted = results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topK)
            .ToList();

        _logger.LogInformation("Semantic retrieval found {Count} matching chunks for query", sorted.Count);
        return sorted;
    }

    public static float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            return 0f;
        }

        float dot = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dot += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        var denom = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        if (denom <= 0.000001f)
        {
            return 0f;
        }

        return Math.Clamp(dot / denom, -1.0f, 1.0f);
    }
}
