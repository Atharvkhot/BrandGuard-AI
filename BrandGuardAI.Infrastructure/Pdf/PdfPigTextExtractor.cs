using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.Common.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace BrandGuardAI.Infrastructure.Pdf;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    private readonly ILogger<PdfPigTextExtractor> _logger;

    public PdfPigTextExtractor(ILogger<PdfPigTextExtractor> logger)
    {
        _logger = logger;
    }

    public Task<List<PdfPageContent>> ExtractPagesAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        var pages = new List<PdfPageContent>();

        try
        {
            if (pdfStream.CanSeek)
            {
                pdfStream.Position = 0;
            }

            using var document = PdfDocument.Open(pdfStream);

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = page.Text ?? string.Empty;
                var trimmed = text.Trim();

                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    pages.Add(new PdfPageContent(page.Number, trimmed));
                }
            }

            _logger.LogInformation("Extracted {PageCount} valid pages from PDF stream", pages.Count);
            return Task.FromResult(pages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PDF document");
            throw new InvalidOperationException($"Unable to parse PDF content: {ex.Message}", ex);
        }
    }
}
