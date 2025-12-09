using System.Collections.Concurrent;
using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0001 // Experimental APIs
#pragma warning disable SKEXP0010 // Experimental APIs
using Microsoft.SemanticKernel.Embeddings;
#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0001

namespace CoralLedger.Infrastructure.AI;

/// <summary>
/// Semantic search service using vector embeddings (Sprint 5.2.5)
/// Provides similarity-based search for marine data queries
/// </summary>
public class SemanticSearchService : ISemanticSearchService
{
    private readonly MarineAIOptions _options;
    private readonly IMarineDbContext _context;
    private readonly ILogger<SemanticSearchService> _logger;
    private readonly Kernel? _kernel;

#pragma warning disable SKEXP0001
    private readonly ITextEmbeddingGenerationService? _embeddingService;
#pragma warning restore SKEXP0001

    // In-memory cache for embeddings (in production, use pgvector or dedicated vector DB)
    private static readonly ConcurrentDictionary<string, CachedEmbedding> _embeddingCache = new();

    // Suggested query templates by category
    private static readonly Dictionary<string, List<string>> QueryTemplates = new()
    {
        ["mpa"] = new()
        {
            "How many Marine Protected Areas are in the Bahamas?",
            "Which MPAs have NoTake protection level?",
            "What is the total protected marine area in the Bahamas?",
            "Show me MPAs near Nassau",
            "Is this location inside any MPA?"
        },
        ["bleaching"] = new()
        {
            "Show me the latest bleaching alerts",
            "Which areas have high bleaching risk?",
            "What is the current DHW?",
            "Show bleaching trends for the past month",
            "Which reefs are showing signs of thermal stress?"
        },
        ["fishing"] = new()
        {
            "What fishing activity has been detected recently?",
            "Show vessels in NoTake zones",
            "Where are fishing vessels concentrated?",
            "Any suspicious fishing activity in the Exumas?",
            "Show fishing patterns for the past week"
        },
        ["reef"] = new()
        {
            "What is the health status of reefs?",
            "Show me healthy reefs",
            "Which reefs have high coral cover?",
            "List reefs showing stress indicators",
            "Compare reef health across island groups"
        }
    };

    public SemanticSearchService(
        IOptions<MarineAIOptions> options,
        IMarineDbContext context,
        ILogger<SemanticSearchService> logger)
    {
        _options = options.Value;
        _context = context;
        _logger = logger;

        if (_options.Enabled && _options.EnableEmbeddings && !string.IsNullOrEmpty(_options.ApiKey))
        {
            try
            {
                var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010
                if (_options.UseAzureOpenAI && !string.IsNullOrEmpty(_options.AzureEndpoint))
                {
                    builder.AddAzureOpenAITextEmbeddingGeneration(
                        deploymentName: _options.EmbeddingModel,
                        endpoint: _options.AzureEndpoint,
                        apiKey: _options.ApiKey);
                }
                else
                {
                    builder.AddOpenAITextEmbeddingGeneration(
                        modelId: _options.EmbeddingModel,
                        apiKey: _options.ApiKey);
                }
#pragma warning restore SKEXP0010

                _kernel = builder.Build();

#pragma warning disable SKEXP0001
                _embeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
#pragma warning restore SKEXP0001

                _logger.LogInformation(
                    "SemanticSearchService initialized with embedding model {Model}",
                    _options.EmbeddingModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SemanticSearchService");
            }
        }
    }

    public bool IsConfigured => _embeddingService != null;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Embedding service not configured, returning empty embedding");
            return Array.Empty<float>();
        }

        // Check cache first
        var cacheKey = GetCacheKey(text);
        if (_embeddingCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            return cached.Embedding;
        }

        try
        {
#pragma warning disable SKEXP0001
            var embeddings = await _embeddingService!.GenerateEmbeddingsAsync(
                new[] { text },
                cancellationToken: cancellationToken);
#pragma warning restore SKEXP0001

            var embedding = embeddings.First().ToArray();

            // Cache for 1 hour
            _embeddingCache[cacheKey] = new CachedEmbedding(embedding, DateTime.UtcNow.AddHours(1));

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text: {Text}", text[..Math.Min(50, text.Length)]);
            return Array.Empty<float>();
        }
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> FindSimilarQueriesAsync(
        string query,
        int maxResults = 5,
        float minSimilarity = 0.7f,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<SemanticSearchResult>();
        }

        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);
        if (queryEmbedding.Length == 0)
        {
            return Array.Empty<SemanticSearchResult>();
        }

        // Get recent successful queries from audit log
        var recentQueries = await _context.NLQAuditLogs
            .Where(q => q.Status == NLQQueryStatus.Completed)
            .OrderByDescending(q => q.QueryTime)
            .Take(100)
            .ToListAsync(cancellationToken);

        var results = new List<SemanticSearchResult>();

        foreach (var auditLog in recentQueries)
        {
            var logEmbedding = await GenerateEmbeddingAsync(auditLog.OriginalQuery, cancellationToken);
            if (logEmbedding.Length == 0) continue;

            var similarity = CosineSimilarity(queryEmbedding, logEmbedding);
            if (similarity >= minSimilarity)
            {
                results.Add(new SemanticSearchResult(
                    auditLog.Id.ToString(),
                    auditLog.OriginalQuery,
                    "Query",
                    similarity,
                    new Dictionary<string, object>
                    {
                        ["InterpretedAs"] = auditLog.InterpretedAs ?? "",
                        ["Persona"] = auditLog.Persona.ToString(),
                        ["QueryTime"] = auditLog.QueryTime
                    }));
            }
        }

        return results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(maxResults)
            .ToList();
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> SearchMarineDataAsync(
        string query,
        string? entityType = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SemanticSearchResult>();

        // If embeddings are not configured, fall back to keyword matching
        if (!IsConfigured)
        {
            return await KeywordSearchAsync(query, entityType, maxResults, cancellationToken);
        }

        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);
        if (queryEmbedding.Length == 0)
        {
            return await KeywordSearchAsync(query, entityType, maxResults, cancellationToken);
        }

        // Search MPAs
        if (entityType == null || entityType.Equals("MPA", StringComparison.OrdinalIgnoreCase))
        {
            var mpas = await _context.MarineProtectedAreas.ToListAsync(cancellationToken);
            foreach (var mpa in mpas)
            {
                var mpaText = $"{mpa.Name} {mpa.IslandGroup} {mpa.ProtectionLevel}";
                var mpaEmbedding = await GenerateEmbeddingAsync(mpaText, cancellationToken);
                if (mpaEmbedding.Length == 0) continue;

                var similarity = CosineSimilarity(queryEmbedding, mpaEmbedding);
                if (similarity >= 0.5f)
                {
                    results.Add(new SemanticSearchResult(
                        mpa.Id.ToString(),
                        mpa.Name,
                        "MPA",
                        similarity,
                        new Dictionary<string, object>
                        {
                            ["IslandGroup"] = mpa.IslandGroup.ToString(),
                            ["ProtectionLevel"] = mpa.ProtectionLevel.ToString(),
                            ["AreaSquareKm"] = mpa.AreaSquareKm
                        }));
                }
            }
        }

        // Search Reefs
        if (entityType == null || entityType.Equals("Reef", StringComparison.OrdinalIgnoreCase))
        {
            var reefs = await _context.Reefs.ToListAsync(cancellationToken);
            foreach (var reef in reefs)
            {
                var reefText = $"{reef.Name} {reef.HealthStatus}";
                var reefEmbedding = await GenerateEmbeddingAsync(reefText, cancellationToken);
                if (reefEmbedding.Length == 0) continue;

                var similarity = CosineSimilarity(queryEmbedding, reefEmbedding);
                if (similarity >= 0.5f)
                {
                    results.Add(new SemanticSearchResult(
                        reef.Id.ToString(),
                        reef.Name,
                        "Reef",
                        similarity,
                        new Dictionary<string, object>
                        {
                            ["HealthStatus"] = reef.HealthStatus.ToString(),
                            ["CoralCoverPercentage"] = reef.CoralCoverPercentage ?? 0
                        }));
                }
            }
        }

        return results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(maxResults)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetContextualSuggestionsAsync(
        string partialQuery,
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        var suggestions = new List<(string query, float score)>();
        var queryLower = partialQuery.ToLowerInvariant();

        // Determine the query category based on keywords
        var category = DetermineQueryCategory(queryLower);

        // Get templates for the category
        if (QueryTemplates.TryGetValue(category, out var templates))
        {
            foreach (var template in templates)
            {
                suggestions.Add((template, 1.0f));
            }
        }

        // If embeddings are configured, also find similar past queries
        if (IsConfigured)
        {
            var similarQueries = await FindSimilarQueriesAsync(
                partialQuery,
                maxSuggestions,
                0.6f,
                cancellationToken);

            foreach (var result in similarQueries)
            {
                suggestions.Add((result.Content, result.SimilarityScore));
            }
        }

        // Return unique suggestions sorted by relevance
        return suggestions
            .GroupBy(s => s.query.ToLowerInvariant())
            .Select(g => g.First())
            .OrderByDescending(s => s.score)
            .Take(maxSuggestions)
            .Select(s => s.query)
            .ToList();
    }

    private static string DetermineQueryCategory(string query)
    {
        if (query.Contains("mpa") || query.Contains("protected area") || query.Contains("boundary"))
            return "mpa";
        if (query.Contains("bleach") || query.Contains("dhw") || query.Contains("temperature") || query.Contains("thermal"))
            return "bleaching";
        if (query.Contains("fish") || query.Contains("vessel") || query.Contains("boat") || query.Contains("catch"))
            return "fishing";
        if (query.Contains("reef") || query.Contains("coral") || query.Contains("health"))
            return "reef";

        return "mpa"; // Default category
    }

    private async Task<IReadOnlyList<SemanticSearchResult>> KeywordSearchAsync(
        string query,
        string? entityType,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<SemanticSearchResult>();
        var queryLower = query.ToLowerInvariant();

        // Simple keyword-based search fallback
        if (entityType == null || entityType.Equals("MPA", StringComparison.OrdinalIgnoreCase))
        {
            var mpas = await _context.MarineProtectedAreas
                .Where(m => m.Name.ToLower().Contains(queryLower))
                .Take(maxResults)
                .ToListAsync(cancellationToken);

            foreach (var mpa in mpas)
            {
                results.Add(new SemanticSearchResult(
                    mpa.Id.ToString(),
                    mpa.Name,
                    "MPA",
                    0.8f, // Fixed score for keyword matches
                    new Dictionary<string, object>
                    {
                        ["IslandGroup"] = mpa.IslandGroup.ToString(),
                        ["ProtectionLevel"] = mpa.ProtectionLevel.ToString()
                    }));
            }
        }

        return results.Take(maxResults).ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0f;

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
        return magnitude == 0 ? 0f : (float)(dotProduct / magnitude);
    }

    private static string GetCacheKey(string text)
    {
        // Simple hash for cache key
        return $"emb_{text.GetHashCode():X8}";
    }

    private record CachedEmbedding(float[] Embedding, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
