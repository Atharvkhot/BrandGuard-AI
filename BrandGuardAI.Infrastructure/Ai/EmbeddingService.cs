using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrandGuardAI.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BrandGuardAI.Infrastructure.Ai;

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmbeddingService> _logger;
    private const int FallbackDimension = 256;

    public EmbeddingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["OPENROUTER_API_KEY"]
                     ?? _configuration["OPENAI_API_KEY"]
                     ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        var embeddingEndpoint = _configuration["AI:EmbeddingEndpoint"];

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(embeddingEndpoint))
        {
            try
            {
                var requestBody = new
                {
                    input = text,
                    model = _configuration["AI:EmbeddingModel"] ?? "text-embedding-3-small"
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, embeddingEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    var embeddingElement = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
                    var vector = new List<float>();
                    foreach (var val in embeddingElement.EnumerateArray())
                    {
                        vector.Add(val.GetSingle());
                    }
                    return Normalize(vector.ToArray());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "External embedding API call failed. Falling back to deterministic semantic vectorizer.");
            }
        }

        // Deterministic high-entropy semantic vector representation
        return GenerateDeterministicVector(text);
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var list = new List<float[]>();
        foreach (var text in texts)
        {
            var emb = await GenerateEmbeddingAsync(text, cancellationToken);
            list.Add(emb);
        }
        return list;
    }

    /// <summary>
    /// Generates a normalized 256-dimensional semantic term & subword vector.
    /// Incorporates unigram, bigram, and stem weights.
    /// </summary>
    public static float[] GenerateDeterministicVector(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[FallbackDimension];
        }

        var vector = new float[FallbackDimension];
        var tokens = Tokenize(text.ToLowerInvariant());

        // Stop words list for high-value semantic focusing
        var stopWords = new HashSet<string> { "the", "and", "a", "an", "in", "on", "of", "to", "for", "with", "at", "by", "from", "is", "are", "was", "were", "it", "this", "that" };

        var filteredTokens = tokens.Where(t => !stopWords.Contains(t)).ToList();

        // Unigrams
        foreach (var token in filteredTokens)
        {
            var stem = token.Length > 5 ? token.Substring(0, 5) : token;
            var hashToken = Math.Abs(GetStableHash(token)) % FallbackDimension;
            var hashStem = Math.Abs(GetStableHash(stem)) % FallbackDimension;

            vector[hashToken] += 3.0f;
            vector[hashStem] += 2.0f;

            // 3-grams
            if (token.Length >= 4)
            {
                for (int i = 0; i <= token.Length - 3; i++)
                {
                    var tri = token.Substring(i, 3);
                    var triHash = Math.Abs(GetStableHash(tri)) % FallbackDimension;
                    vector[triHash] += 0.8f;
                }
            }
        }

        // Bigrams
        for (int i = 0; i < filteredTokens.Count - 1; i++)
        {
            var bigram = $"{filteredTokens[i]}_{filteredTokens[i + 1]}";
            var biHash = Math.Abs(GetStableHash(bigram)) % FallbackDimension;
            vector[biHash] += 4.0f;
        }

        return Normalize(vector);
    }

    private static List<string> Tokenize(string text)
    {
        var punctuation = text.Select(c => char.IsPunctuation(c) ? ' ' : c).ToArray();
        return new string(punctuation)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 1)
            .ToList();
    }

    private static int GetStableHash(string s)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
        return BitConverter.ToInt32(bytes, 0);
    }

    private static float[] Normalize(float[] vector)
    {
        var sumSquares = 0f;
        for (int i = 0; i < vector.Length; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        var magnitude = (float)Math.Sqrt(sumSquares);
        if (magnitude <= 0.000001f)
        {
            return vector;
        }

        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / magnitude;
        }

        return normalized;
    }
}
