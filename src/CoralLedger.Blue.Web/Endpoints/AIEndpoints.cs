using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Enums;

namespace CoralLedger.Blue.Web.Endpoints;

/// <summary>
/// API endpoints for AI-powered natural language queries
/// Sprint 5.1: Enhanced with two-step query flow (interpret -> confirm -> execute)
/// </summary>
public static class AIEndpoints
{
    public static IEndpointRouteBuilder MapAIEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ai")
            .WithTags("AI Assistant");

        // GET /api/ai/status - Check if AI is configured
        group.MapGet("/status", (IMarineAIService aiService) =>
        {
            return Results.Ok(new
            {
                configured = aiService.IsConfigured,
                message = aiService.IsConfigured
                    ? "AI assistant is ready"
                    : "AI assistant is not configured. Set MarineAI:ApiKey in configuration."
            });
        })
        .WithName("GetAIStatus")
        .WithDescription("Check if AI service is configured and available")
        .Produces<object>();

        // POST /api/ai/interpret - Interpret a query before execution (US-5.1.2)
        // Returns what the AI understood and any disambiguation needed
        group.MapPost("/interpret", async (
            AIQueryRequest request,
            IMarineAIService aiService,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest(new { error = "Query is required" });
            }

            if (request.Query.Length > 500)
            {
                return Results.BadRequest(new { error = "Query must be 500 characters or less" });
            }

            var persona = request.Persona ?? UserPersona.General;
            var interpretation = await aiService.InterpretQueryAsync(request.Query, persona, ct).ConfigureAwait(false);

            // Check for security warning
            if (interpretation.SecurityWarning != null)
            {
                return Results.Json(new
                {
                    success = false,
                    error = interpretation.SecurityWarning,
                    interpretationId = interpretation.InterpretationId
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(new
            {
                success = true,
                interpretationId = interpretation.InterpretationId,
                originalQuery = interpretation.OriginalQuery,
                interpretedAs = interpretation.InterpretedAs,
                dataSources = interpretation.DataSourcesUsed,
                requiresDisambiguation = interpretation.RequiresDisambiguation,
                disambiguationOptions = interpretation.DisambiguationNeeded?.Select(d => new
                {
                    vagueTerm = d.VagueTerm,
                    question = d.Question,
                    options = d.Options
                }),
                persona = interpretation.Persona.ToString(),
                confirmationMessage = interpretation.RequiresDisambiguation
                    ? "Your query contains vague terms. Please clarify the options above, or proceed with default interpretations."
                    : $"I understood this as: {interpretation.InterpretedAs}. Ready to execute?"
            });
        })
        .WithName("InterpretAIQuery")
        .WithDescription("Interpret a natural language query and show what the AI understood before execution (Sprint 5.1 US-5.1.2)")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden);

        // POST /api/ai/execute/{interpretationId} - Execute a previously interpreted query
        group.MapPost("/execute/{interpretationId}", async (
            string interpretationId,
            IMarineAIService aiService,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(interpretationId))
            {
                return Results.BadRequest(new { error = "Interpretation ID is required" });
            }

            var result = await aiService.ExecuteInterpretedQueryAsync(interpretationId, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(new
            {
                success = true,
                answer = result.Answer,
                interpretedAs = result.InterpretedAs,
                persona = result.Persona.ToString(),
                data = result.Data
            });
        })
        .WithName("ExecuteInterpretedQuery")
        .WithDescription("Execute a previously interpreted query after user confirmation")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest);

        // POST /api/ai/query - Direct query (for backwards compatibility and simple queries)
        group.MapPost("/query", async (
            AIQueryRequest request,
            IMarineAIService aiService,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest(new { error = "Query is required" });
            }

            if (request.Query.Length > 500)
            {
                return Results.BadRequest(new { error = "Query must be 500 characters or less" });
            }

            var persona = request.Persona ?? UserPersona.General;
            var result = await aiService.QueryAsync(request.Query, persona, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                // Check if it's a security restriction
                if (result.Error?.Contains("restricted") == true)
                {
                    return Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden);
                }
                return Results.BadRequest(new { error = result.Error });
            }

            return Results.Ok(new
            {
                query = request.Query,
                persona = result.Persona.ToString(),
                answer = result.Answer,
                interpretedAs = result.InterpretedAs,
                data = result.Data
            });
        })
        .WithName("QueryAI")
        .WithDescription("Submit a natural language query about marine data with optional persona (General, Ranger, Fisherman, Scientist, Policymaker)")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/ai/personas - Get available personas
        group.MapGet("/personas", () =>
        {
            var personas = Enum.GetValues<UserPersona>()
                .Select(p => new
                {
                    value = p.ToString(),
                    description = GetPersonaDescription(p)
                });
            return Results.Ok(personas);
        })
        .WithName("GetAIPersonas")
        .WithDescription("Get available user personas for response formatting")
        .Produces<object>();

        // GET /api/ai/suggestions - Get suggested queries
        group.MapGet("/suggestions", async (
            IMarineAIService aiService,
            CancellationToken ct = default) =>
        {
            var suggestions = await aiService.GetSuggestedQueriesAsync(ct).ConfigureAwait(false);
            return Results.Ok(new { suggestions });
        })
        .WithName("GetAISuggestions")
        .WithDescription("Get suggested natural language queries")
        .Produces<object>();

        // GET /api/ai/vague-terms - Get list of vague terms that trigger disambiguation
        group.MapGet("/vague-terms", () =>
        {
            var vagueTerms = new[]
            {
                new { term = "healthy", description = "Ambiguous reef health indicator - could mean low bleaching, high coral cover, or active fish population" },
                new { term = "recent", description = "Ambiguous time period - could mean 24 hours, 7 days, or 30 days" },
                new { term = "nearby", description = "Ambiguous distance - could mean 5km, 20km, or same island group" },
                new { term = "high risk", description = "Ambiguous bleaching threshold - could mean DHW > 4, Alert Level 2+, or any watch" },
                new { term = "stressed", description = "Ambiguous coral stress indicator - could mean bleaching watch, SST anomaly, or elevated DHW" },
                new { term = "active", description = "Ambiguous time period for activity - could mean current, 24 hours, or past week" }
            };
            return Results.Ok(new
            {
                description = "These terms in queries will trigger disambiguation prompts (US-5.1.4)",
                terms = vagueTerms
            });
        })
        .WithName("GetVagueTerms")
        .WithDescription("Get list of vague terms that will trigger disambiguation prompts in queries")
        .Produces<object>();

        // Sprint 5.2.5: Semantic Search Endpoints

        // GET /api/ai/semantic/status - Check if semantic search is configured
        group.MapGet("/semantic/status", (ISemanticSearchService searchService) =>
        {
            return Results.Ok(new
            {
                configured = searchService.IsConfigured,
                message = searchService.IsConfigured
                    ? "Semantic search with vector embeddings is enabled"
                    : "Semantic search is not configured. Set MarineAI:EnableEmbeddings in configuration."
            });
        })
        .WithName("GetSemanticSearchStatus")
        .WithDescription("Check if semantic search with vector embeddings is configured (Sprint 5.2.5)")
        .Produces<object>();

        // POST /api/ai/semantic/search - Search marine data using semantic similarity
        group.MapPost("/semantic/search", async (
            SemanticSearchRequest request,
            ISemanticSearchService searchService,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest(new { error = "Query is required" });
            }

            var results = await searchService.SearchMarineDataAsync(
                request.Query,
                request.EntityType,
                request.MaxResults ?? 10,
                ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                query = request.Query,
                entityType = request.EntityType ?? "All",
                resultCount = results.Count,
                results = results.Select(r => new
                {
                    id = r.Id,
                    content = r.Content,
                    entityType = r.EntityType,
                    similarityScore = r.SimilarityScore,
                    metadata = r.Metadata
                })
            });
        })
        .WithName("SemanticSearch")
        .WithDescription("Search marine data using vector embeddings and semantic similarity")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest);

        // GET /api/ai/semantic/suggestions - Get contextual query suggestions
        group.MapGet("/semantic/suggestions", async (
            string? partialQuery,
            ISemanticSearchService searchService,
            CancellationToken ct = default) =>
        {
            var suggestions = await searchService.GetContextualSuggestionsAsync(
                partialQuery ?? "",
                maxSuggestions: 5,
                ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                partialQuery = partialQuery ?? "",
                suggestions
            });
        })
        .WithName("GetSemanticSuggestions")
        .WithDescription("Get contextual query suggestions based on partial input and similar past queries")
        .Produces<object>();

        // POST /api/ai/semantic/similar - Find similar past queries
        group.MapPost("/semantic/similar", async (
            SemanticSearchRequest request,
            ISemanticSearchService searchService,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest(new { error = "Query is required" });
            }

            var similarQueries = await searchService.FindSimilarQueriesAsync(
                request.Query,
                request.MaxResults ?? 5,
                request.MinSimilarity ?? 0.7f,
                ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                query = request.Query,
                resultCount = similarQueries.Count,
                similarQueries = similarQueries.Select(r => new
                {
                    originalQuery = r.Content,
                    similarityScore = r.SimilarityScore,
                    interpretedAs = r.Metadata?.GetValueOrDefault("InterpretedAs"),
                    persona = r.Metadata?.GetValueOrDefault("Persona")
                })
            });
        })
        .WithName("FindSimilarQueries")
        .WithDescription("Find similar past queries using vector embeddings")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static string GetPersonaDescription(UserPersona persona) => persona switch
    {
        UserPersona.General => "Default balanced response for general users",
        UserPersona.Ranger => "Park ranger - Focus on enforcement, patrol routes, violations",
        UserPersona.Fisherman => "Commercial fisherman - Focus on sustainability, quotas, plain language",
        UserPersona.Scientist => "Researcher - Include data sources, methodology, statistics",
        UserPersona.Policymaker => "Government official - Executive summary, policy implications",
        _ => persona.ToString()
    };
}

public record AIQueryRequest(string Query, UserPersona? Persona = null);

// Sprint 5.2.5: Request for semantic search endpoints
public record SemanticSearchRequest(
    string Query,
    string? EntityType = null,
    int? MaxResults = null,
    float? MinSimilarity = null);
