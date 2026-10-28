using BrandGuardAI.Infrastructure.Ai;
using BrandGuardAI.Infrastructure.Retrieval;
using FluentAssertions;
using Xunit;

namespace BrandGuardAI.Tests;

public class EmbeddingAndSemanticRetrievalTests
{
    [Fact]
    public void GenerateDeterministicVector_ShouldProduceNormalizedUnitVector()
    {
        // Arrange
        var text = "Our product guarantees 100% results across all deployments.";

        // Act
        var vector = EmbeddingService.GenerateDeterministicVector(text);

        // Assert
        vector.Should().HaveCount(256);

        // Calculate L2 norm
        float sumSquares = 0f;
        foreach (var val in vector)
        {
            sumSquares += val * val;
        }
        var magnitude = Math.Sqrt(sumSquares);

        magnitude.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_ShouldBeHighForRelatedContent_AndLowForUnrelated()
    {
        // Arrange
        var textA = "We strictly prohibit absolute outcome guarantees and 100% success promises in marketing.";
        var textB = "Our solution guarantees 100% results without any risk.";
        var textUnrelated = "The recipe requires two cups of flour and one tablespoon of vanilla extract.";

        var vecA = EmbeddingService.GenerateDeterministicVector(textA);
        var vecB = EmbeddingService.GenerateDeterministicVector(textB);
        var vecUnrelated = EmbeddingService.GenerateDeterministicVector(textUnrelated);

        // Act
        var similarityRelated = SemanticSearchService.CalculateCosineSimilarity(vecA, vecB);
        var similarityUnrelated = SemanticSearchService.CalculateCosineSimilarity(vecA, vecUnrelated);

        // Assert
        similarityRelated.Should().BeGreaterThan(0.20f);
        similarityUnrelated.Should().BeLessThan(0.15f);
        similarityRelated.Should().BeGreaterThan(similarityUnrelated);
    }

    [Fact]
    public void IdenticalContent_ShouldHaveCosineSimilarityApproximatelyOne()
    {
        // Arrange
        var text = "Brand governance and marketing compliance standard.";
        var vecA = EmbeddingService.GenerateDeterministicVector(text);
        var vecB = EmbeddingService.GenerateDeterministicVector(text);

        // Act
        var similarity = SemanticSearchService.CalculateCosineSimilarity(vecA, vecB);

        // Assert
        similarity.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void EmptyOrWhitespaceInput_ShouldProduceSafeValidVector()
    {
        // Arrange
        var emptyText = "   ";

        // Act
        var vector = EmbeddingService.GenerateDeterministicVector(emptyText);

        // Assert
        vector.Should().NotBeNull();
        vector.Should().HaveCount(256);
        vector.All(v => !float.IsNaN(v) && !float.IsInfinity(v)).Should().BeTrue();
    }
}
