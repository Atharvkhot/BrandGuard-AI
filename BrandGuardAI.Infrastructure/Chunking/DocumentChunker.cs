using System.Text.RegularExpressions;
using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;

namespace BrandGuardAI.Infrastructure.Chunking;

public class DocumentChunker : IDocumentChunker
{
    private static readonly Regex SectionHeadingRegex = new(
        @"^([0-9]+(\.[0-9]+)*\s+[A-Z][^\n]{3,60}|[A-Z\s]{4,40}:?)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public List<DocumentChunk> ChunkPages(List<PdfPageContent> pages, int maxChunkSize = 600, int chunkOverlap = 100)
    {
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        foreach (var page in pages)
        {
            var rawText = page.Text.Replace("\r\n", "\n").Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                continue;
            }

            // Split into paragraphs
            var paragraphs = rawText.Split(new[] { "\n\n", "\n \n" }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(p => p.Trim())
                                    .Where(p => !string.IsNullOrWhiteSpace(p))
                                    .ToList();

            var currentSectionTitle = $"Page {page.PageNumber}";
            var currentBuffer = new System.Text.StringBuilder();

            foreach (var paragraph in paragraphs)
            {
                // Detect section headings
                var firstLine = paragraph.Split('\n')[0].Trim();
                if (SectionHeadingRegex.IsMatch(firstLine) && firstLine.Length < 70)
                {
                    currentSectionTitle = firstLine;
                }

                if (currentBuffer.Length + paragraph.Length > maxChunkSize && currentBuffer.Length > 0)
                {
                    var chunkText = currentBuffer.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(chunkText))
                    {
                        chunks.Add(new DocumentChunk(
                            chunkIndex++,
                            chunkText,
                            page.PageNumber,
                            currentSectionTitle
                        ));
                    }

                    // Handle overlap
                    var overlapText = chunkText.Length > chunkOverlap
                        ? chunkText[^chunkOverlap..]
                        : string.Empty;

                    currentBuffer.Clear();
                    if (!string.IsNullOrWhiteSpace(overlapText))
                    {
                        currentBuffer.Append(overlapText).Append(' ');
                    }
                }

                currentBuffer.Append(paragraph).Append("\n\n");
            }

            if (currentBuffer.Length > 0)
            {
                var remainingText = currentBuffer.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(remainingText))
                {
                    chunks.Add(new DocumentChunk(
                        chunkIndex++,
                        remainingText,
                        page.PageNumber,
                        currentSectionTitle
                    ));
                }
            }
        }

        return chunks;
    }
}
