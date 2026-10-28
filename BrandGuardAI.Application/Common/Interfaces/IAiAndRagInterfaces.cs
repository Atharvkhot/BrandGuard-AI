using BrandGuardAI.Application.Common.Models;

namespace BrandGuardAI.Application.Common.Interfaces;

public interface IPdfTextExtractor
{
    Task<List<PdfPageContent>> ExtractPagesAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}

public interface IDocumentChunker
{
    List<DocumentChunk> ChunkPages(List<PdfPageContent> pages, int maxChunkSize = 600, int chunkOverlap = 100);
}

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}

public interface ISemanticSearchService
{
    Task<List<ChunkSearchResult>> SearchSimilarChunksAsync(
        string query,
        int topK = 5,
        float minSimilarity = 0.25f,
        CancellationToken cancellationToken = default);
}
