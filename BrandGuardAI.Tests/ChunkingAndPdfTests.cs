using System.Text;
using BrandGuardAI.Application.Common.Models;
using BrandGuardAI.Infrastructure.Chunking;
using BrandGuardAI.Infrastructure.Pdf;
using BrandGuardAI.Infrastructure.Utils;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrandGuardAI.Tests;

public class ChunkingAndPdfTests
{
    [Fact]
    public async Task PdfPigTextExtractor_ShouldExtractTextAndPreservePages()
    {
        // Arrange
        var pdfBytes = SamplePdfGenerator.CreateSampleGuidelinePdf();
        using var stream = new MemoryStream(pdfBytes);
        var extractor = new PdfPigTextExtractor(NullLogger<PdfPigTextExtractor>.Instance);

        // Act
        var pages = await extractor.ExtractPagesAsync(stream);

        // Assert
        pages.Should().NotBeNull();
        pages.Should().HaveCountGreaterThanOrEqualTo(1);
        pages[0].PageNumber.Should().Be(1);
        pages[0].Text.Should().Contain("BRANDGUARD");
        pages[0].Text.Should().Contain("Absolute Guarantees Prohibited");
    }

    [Fact]
    public void DocumentChunker_ShouldPreservePageNumbersAndSectionTitles()
    {
        // Arrange
        var chunker = new DocumentChunker();
        var pages = new List<PdfPageContent>
        {
            new(1, "SECTION 1: BRAND IDENTITY\n\nAll copy must adhere to corporate tone guidelines.\n\nNever promise guaranteed results under any circumstance.")
        };

        // Act
        var chunks = chunker.ChunkPages(pages, maxChunkSize: 200, chunkOverlap: 20);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.All(c => c.PageNumber == 1).Should().BeTrue();
        chunks.Any(c => c.Text.Contains("corporate tone")).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAndVerifyQaApprovedGuidelinePdf_ShouldContainAllFiveMandatoryRules()
    {
        // Arrange
        var sb = new StringBuilder();
        sb.AppendLine("BRANDGUARD OFFICIAL MARKETING & BRAND GOVERNANCE POLICIES (2026)");
        sb.AppendLine();
        sb.AppendLine("Section 1: General Tone and Verification Directives");
        sb.AppendLine("RULE 4: Marketing content must use professional and respectful language.");
        sb.AppendLine("All campaign copy, customer communications, and public advertisements must maintain an authoritative, dignified, and courteous tone. Emotional manipulation and deceptive hype are strictly forbidden.");
        sb.AppendLine();
        sb.AppendLine("RULE 5: Product benefits must be supported by approved information.");
        sb.AppendLine("Every statement asserting technical capabilities, operational speed, or efficacy must be backed by documented evidence approved by the product engineering council.");
        sb.AppendLine();
        sb.AppendLine("Section 2: Outcome Prohibitions and Superiority Restrictions");
        sb.AppendLine("RULE 1: Marketing content must not promise guaranteed results.");
        sb.AppendLine("Organizations and marketing authors must never assure universal outcomes or infallible success. Performance fluctuates based on customer operational environments.");
        sb.AppendLine();
        sb.AppendLine("RULE 2: Absolute performance claims such as \"100% guaranteed\" are prohibited.");
        sb.AppendLine("Phrases including '100% guaranteed', 'guarantees 100% results', 'never fails', and 'risk-free' are strictly prohibited in marketing and sales materials.");
        sb.AppendLine();
        sb.AppendLine("RULE 3: Unverified claims that a product is the best in the world are prohibited.");
        sb.AppendLine("Superlative market leadership assertions such as 'best solution in the world' or '#1 in the world' must be supported by verified, accredited third-party audited proof before public distribution.");

        var text = sb.ToString();
        var pdfBytes = SamplePdfGenerator.CreateSimplePdf(text);

        Directory.CreateDirectory("samples");
        var outputPath = Path.Combine("samples", "BrandGuard_QA_Approved_Guidelines.pdf");
        await File.WriteAllBytesAsync(outputPath, pdfBytes);

        // Act
        using var stream = new MemoryStream(pdfBytes);
        var extractor = new PdfPigTextExtractor(NullLogger<PdfPigTextExtractor>.Instance);
        var pages = await extractor.ExtractPagesAsync(stream);

        // Assert
        pages.Should().NotBeNullOrEmpty();
        var extracted = pages[0].Text;
        extracted.Should().Contain("RULE 1: Marketing content must not promise guaranteed results.");
        extracted.Should().Contain("RULE 2: Absolute performance claims such as");
        extracted.Should().Contain("RULE 3: Unverified claims that a product is the best in the world are prohibited.");
        extracted.Should().Contain("RULE 4: Marketing content must use professional and respectful language.");
        extracted.Should().Contain("RULE 5: Product benefits must be supported by approved information.");
    }
}
