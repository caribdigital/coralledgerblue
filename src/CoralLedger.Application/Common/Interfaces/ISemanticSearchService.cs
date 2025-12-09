namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// Semantic search service for vector embeddings (Sprint 5.2.5)
/// Enables similarity-based search for marine data queries
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Whether the service is configured and operational
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Generate a vector embedding for a text input
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find similar queries from the query history
    /// </summary>
    Task<IReadOnlyList<SemanticSearchResult>> FindSimilarQueriesAsync(
        string query,
        int maxResults = 5,
        float minSimilarity = 0.7f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find relevant MPA or reef data based on semantic similarity
    /// </summary>
    Task<IReadOnlyList<SemanticSearchResult>> SearchMarineDataAsync(
        string query,
        string? entityType = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get contextually relevant suggested queries based on a partial query
    /// </summary>
    Task<IReadOnlyList<string>> GetContextualSuggestionsAsync(
        string partialQuery,
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from a semantic similarity search
/// </summary>
public record SemanticSearchResult(
    string Id,
    string Content,
    string EntityType,
    float SimilarityScore,
    Dictionary<string, object>? Metadata = null);
