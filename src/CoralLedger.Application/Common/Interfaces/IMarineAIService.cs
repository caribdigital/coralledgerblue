using CoralLedger.Domain.Enums;

namespace CoralLedger.Application.Common.Interfaces;

/// <summary>
/// AI service for natural language queries about marine data
/// </summary>
public interface IMarineAIService
{
    /// <summary>
    /// Whether AI features are configured and available
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Process a natural language query about marine data
    /// </summary>
    Task<MarineQueryResult> QueryAsync(
        string naturalLanguageQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a natural language query with persona-aware formatting
    /// </summary>
    Task<MarineQueryResult> QueryAsync(
        string naturalLanguageQuery,
        UserPersona persona,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get suggested queries based on context
    /// </summary>
    Task<IReadOnlyList<string>> GetSuggestedQueriesAsync(
        CancellationToken cancellationToken = default);
}

public record MarineQueryResult(
    bool Success,
    string? Answer = null,
    MarineQueryData? Data = null,
    UserPersona Persona = UserPersona.General,
    string? SqlGenerated = null,
    string? Error = null);

public record MarineQueryData(
    string DataType,
    object? Results = null,
    int? Count = null);
