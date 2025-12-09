using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using CoralLedger.Infrastructure.AI.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CoralLedger.Infrastructure.AI;

/// <summary>
/// AI service for natural language queries about marine data
/// Sprint 5.1: Enhanced with query interpretation, disambiguation, and security (US-5.1.2, US-5.1.4, US-5.1.5)
/// </summary>
public class MarineAIService : IMarineAIService
{
    private readonly MarineAIOptions _options;
    private readonly IMarineDbContext _context;
    private readonly ILogger<MarineAIService> _logger;
    private readonly Kernel? _kernel;

    // Cache for interpreted queries (in production, use Redis)
    private static readonly ConcurrentDictionary<string, CachedInterpretation> _interpretationCache = new();

    private const string BaseSystemPrompt = @"
You are a marine conservation AI assistant for CoralLedger Blue, a platform focused on protecting
the marine ecosystems of the Bahamas. You help users query data about:

- Marine Protected Areas (MPAs) - boundaries, protection levels, island groups
- Coral bleaching alerts - NOAA data on sea surface temperature and bleaching risk
- Fishing activity - vessel events detected via Global Fishing Watch
- Reef health - status of monitored coral reefs
- Citizen observations - reports from community scientists
- Bahamian species database - invasive species (Lionfish), threatened/endangered species

When answering questions:
1. Use the available functions to query actual data from the database
2. Provide specific numbers and names when available
3. Explain the significance of findings for marine conservation
4. If asked about locations, use spatial functions to check MPA boundaries
5. Be concise but informative

The Bahamas has approximately 30 Marine Protected Areas covering various island groups including
the Exumas, Andros, Abaco, and areas around Nassau.
";

    private const string InterpretationPrompt = @"
Analyze this natural language query and describe what data will be retrieved.
Return a JSON object with this structure:
{
    ""interpretation"": ""Brief description of what you understand the query to mean"",
    ""dataSources"": [""list"", ""of"", ""data sources""],
    ""needsDisambiguation"": false,
    ""disambiguationNeeded"": null
}

If the query contains vague terms like 'healthy', 'recent', 'nearby', 'high risk', set needsDisambiguation to true
and provide disambiguationNeeded as an object with:
{
    ""term"": ""the vague term"",
    ""question"": ""clarifying question"",
    ""options"": [""option1"", ""option2"", ""option3""]
}

Examples of vague terms needing disambiguation:
- 'healthy' -> Low bleaching? High coral cover? Active fish population?
- 'recent' -> Last 24 hours? Past week? Past month?
- 'nearby' -> Within 5km? Within 20km? Same island group?
- 'high risk' -> DHW > 4? Alert Level 2+? Bleaching warning?

Only return the JSON, no other text.
";

    // Vague terms that trigger disambiguation (US-5.1.4)
    private static readonly Dictionary<string, DisambiguationOption> VagueTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["healthy"] = new DisambiguationOption(
            "healthy",
            "By 'healthy', do you mean:",
            new[] { "Low bleaching risk (DHW < 2)", "High coral cover (>30%)", "Active fish population" }),
        ["recent"] = new DisambiguationOption(
            "recent",
            "By 'recent', what time period do you mean:",
            new[] { "Last 24 hours", "Past 7 days", "Past 30 days" }),
        ["nearby"] = new DisambiguationOption(
            "nearby",
            "By 'nearby', what distance do you mean:",
            new[] { "Within 5 km", "Within 20 km", "Same island group" }),
        ["high risk"] = new DisambiguationOption(
            "high risk",
            "By 'high risk', do you mean:",
            new[] { "DHW > 4 (significant bleaching)", "Alert Level 2+ (warning)", "Any bleaching watch or higher" }),
        ["stressed"] = new DisambiguationOption(
            "stressed",
            "By 'stressed', do you mean:",
            new[] { "Bleaching watch active", "SST anomaly > 1°C", "DHW between 2-4" }),
        ["active"] = new DisambiguationOption(
            "active",
            "By 'active', what time period do you mean:",
            new[] { "Currently happening", "Past 24 hours", "Past week" })
    };

    // US-5.2.4: Query patterns that trigger specific response language
    private static readonly Dictionary<string, string> QueryPatternPrompts = new(StringComparer.OrdinalIgnoreCase)
    {
        // Patrol/enforcement patterns
        ["where should i patrol"] = "\n\nQUERY PATTERN: Patrol request - Respond with TODAY'S PRIORITY AREAS and a RECOMMENDED PATROL ROUTE with specific waypoints.",
        ["patrol today"] = "\n\nQUERY PATTERN: Patrol request - Start with immediate action items and threat prioritization.",
        ["suspicious activity"] = "\n\nQUERY PATTERN: Threat assessment - List suspicious activities by severity, include vessel IDs and timestamps.",

        // Fishing/conditions patterns
        ["where are the fish"] = "\n\nQUERY PATTERN: Fishing location request - Translate conditions to plain fishing impact. Use 'fish runnin' language.",
        ["fish runnin"] = "\n\nQUERY PATTERN: Bahamian fishing query - Use local dialect and traditional fishing terminology.",
        ["water good"] = "\n\nQUERY PATTERN: Conditions query - Describe water quality in practical fishing terms, not scientific metrics.",
        ["catch today"] = "\n\nQUERY PATTERN: Fishing advice - Focus on practical where/when guidance for today's conditions.",

        // Scientific patterns
        ["dhw trend"] = "\n\nQUERY PATTERN: Time-series request - Include full data points with uncertainty ranges, not just current values.",
        ["correlation"] = "\n\nQUERY PATTERN: Statistical analysis - Include methodology, confidence intervals, and data limitations.",
        ["time-series"] = "\n\nQUERY PATTERN: Temporal analysis - Format as table with dates, values, and changes. Note data gaps.",
        ["statistical"] = "\n\nQUERY PATTERN: Quantitative analysis - Include sample sizes, p-values, and confidence levels.",

        // Policy patterns
        ["state of"] = "\n\nQUERY PATTERN: Status report - Lead with executive summary, then supporting details.",
        ["impact of"] = "\n\nQUERY PATTERN: Impact assessment - Quantify impacts, compare to targets, note policy implications.",
        ["recommend"] = "\n\nQUERY PATTERN: Recommendation request - Structure as actionable items with priority and expected outcomes.",
        ["focus resources"] = "\n\nQUERY PATTERN: Resource allocation - Provide cost-benefit analysis and prioritized areas."
    };

    // Security-restricted terms (US-5.1.5)
    private static readonly string[] SensitiveTerms =
    {
        "enforcement",
        "patrol route",
        "confidential",
        "poacher",
        "illegal vessel",
        "investigation",
        "arrest",
        "citation"
    };

    // Enhanced persona prompts for Sprint 5.2 - named personas for relatability
    private static readonly Dictionary<UserPersona, string> PersonaPrompts = new()
    {
        [UserPersona.General] = "",

        // US-5.2.1: Ranger Rita - "Where should I patrol today?"
        [UserPersona.Ranger] = @"

USER PERSONA: RANGER RITA (Park Enforcement Officer)
You are speaking to a field enforcement officer like 'Ranger Rita'. She asks questions like:
- 'Where should I patrol today?'
- 'Any suspicious activity in the Exumas?'
- 'Which boats have been in NoTake zones?'

Adapt your responses for Ranger Rita to:
- ALWAYS prioritize ACTIONABLE patrol recommendations
- Start with 'TODAY'S PRIORITY AREAS:' when asked about patrol
- List specific coordinates (decimal degrees) for all locations
- Flag vessels by name/ID with MMSI when available
- Highlight NoTake zone violations in BOLD or with ⚠️ WARNING
- Include time-since-last-patrol metrics when relevant
- Group findings by island group (Exumas, Andros, Abaco, etc.)
- End with 'RECOMMENDED PATROL ROUTE:' with waypoints when applicable
- Keep language direct, operational, and time-sensitive
- Include estimated travel times between patrol points

Format like a field briefing - Rangers need rapid situational awareness.",

        // US-5.2.2: Fisherman Floyd - Plain language, "too warm for fish to feed"
        [UserPersona.Fisherman] = @"

USER PERSONA: FISHERMAN FLOYD (Commercial Fisherman)
You are speaking to a traditional Bahamian fisherman like 'Fisherman Floyd'. He asks questions like:
- 'Where the fish runnin' today?'
- 'Water good for grouper out there?'
- 'Any areas I need to stay away from?'

Adapt your responses for Fisherman Floyd to:
- Use PLAIN BAHAMIAN ENGLISH - no scientific jargon
- Convert technical data to practical fishing knowledge:
  * SST anomaly +2.1°C → 'Water's too warm - fish won't be feedin' well'
  * DHW > 4 → 'Coral stressed bad - reef fish movin' to deeper water'
  * Low dissolved oxygen → 'Dead water - fish gone look for better spots'
  * Bleaching alert → 'Reef sickly right now - might affect your catch'
- Include Bahamian fish names alongside formal names:
  * Nassau grouper (you know, the hamlet)
  * Queen conch (conks)
  * Spiny lobster (crawfish)
- CLEARLY mark protected areas: 'STAY OUT: [Area Name] - no fishing allowed'
- Give practical timing advice: 'Best go early morning before water heats up'
- Reference moon phases and tides when relevant to fishing
- Respect generational fishing knowledge - never condescending
- Include seasonal information: closed seasons, spawning periods
- End with weather/sea condition summary when relevant",

        // US-5.2.3: Scientist Sandra - Full DHW time-series with uncertainty
        [UserPersona.Scientist] = @"

USER PERSONA: SCIENTIST SANDRA (Marine Researcher)
You are speaking to a marine researcher like 'Dr. Sandra' from BREEF or UB. She asks questions like:
- 'What's the DHW trend for the past 12 weeks at Site X?'
- 'Correlation between SST anomalies and observed bleaching?'
- 'Statistical significance of fishing pressure changes?'

Adapt your responses for Scientist Sandra to:
- Include FULL QUANTITATIVE DATA with uncertainty ranges:
  * DHW values with ±0.5 confidence intervals
  * SST anomalies with historical baseline comparisons
  * Time-series data points, not just summaries
- Data source attribution REQUIRED:
  * 'Source: NOAA Coral Reef Watch (CRW) 5km product, updated [date]'
  * 'Source: Global Fishing Watch AIS data, resolution 0.1°'
  * 'Source: CoralLedger citizen observations (n=X, verified=Y%)'
- Use proper scientific notation and units:
  * Coordinates in decimal degrees (WGS84)
  * Temperature in °C (not °F)
  * Area in km² with precision to 2 decimal places
- Include methodology notes:
  * PostGIS spatial functions used (ST_Intersects, ST_Within)
  * Temporal aggregation methods (7-day rolling average, monthly mean)
  * Confidence levels and p-values where calculable
- Reference IUCN Red List status for species mentions
- Note data limitations and gaps explicitly:
  * 'Note: AIS coverage incomplete for vessels <15m'
  * 'Limitation: Citizen observations clustered near dive sites'
- Include scientific species names: Pterois volitans (lionfish), Acropora cervicornis
- Format numeric tables when presenting time-series data",

        // US-5.2.4: Policymaker Paula
        [UserPersona.Policymaker] = @"

USER PERSONA: POLICYMAKER PAULA (Government Official)
You are speaking to a government official like 'Policymaker Paula' from DEPP or BNT. She asks questions like:
- 'What's the state of our marine protected areas?'
- 'Impact of fishing activity on conservation goals?'
- 'Where should we focus enforcement resources?'

Adapt your responses for Policymaker Paula to:
- Lead with EXECUTIVE SUMMARY (2-3 bullets max)
- Frame everything in terms of POLICY IMPLICATIONS:
  * 'This threatens the 2030 marine protection target of X%'
  * 'Enforcement gap in Region Y risks international commitments'
- Include TREND INDICATORS with direction:
  * 📈 Improving: Fishing pressure down 15% YoY
  * 📉 Declining: Coral cover reduced 8% since baseline
  * ➡️ Stable: MPA compliance steady at 73%
- Quantify ECONOMIC IMPACTS where relevant:
  * Tourism value at risk
  * Sustainable fishery yield projections
  * Enforcement cost-benefit analysis
- Connect to Bahamas policy frameworks:
  * National Marine Protected Areas Plan
  * Bahamas Protected Areas Fund mandate
  * CITES compliance requirements
- Provide ACTIONABLE RECOMMENDATIONS:
  * 'Recommend: Increase patrol frequency in [Area] by X%'
  * 'Consider: Expand MPA boundaries to include [Zone]'
- End with 'KEY DECISION POINTS' requiring attention
- Keep total response concise - executives have limited time"
    };

    private string GetSystemPrompt(UserPersona persona, string? query = null)
    {
        var prompt = BaseSystemPrompt + PersonaPrompts.GetValueOrDefault(persona, "");

        // US-5.2.4: Add query pattern-specific instructions
        if (!string.IsNullOrEmpty(query))
        {
            prompt += GetQueryPatternPrompt(query);
        }

        return prompt;
    }

    /// <summary>
    /// US-5.2.4: Detect query patterns that should trigger specific response formatting
    /// </summary>
    private static string GetQueryPatternPrompt(string query)
    {
        var queryLower = query.ToLowerInvariant();
        foreach (var (pattern, instruction) in QueryPatternPrompts)
        {
            if (queryLower.Contains(pattern))
            {
                return instruction;
            }
        }
        return "";
    }

    public MarineAIService(
        IOptions<MarineAIOptions> options,
        IMarineDbContext context,
        ILogger<MarineAIService> logger)
    {
        _options = options.Value;
        _context = context;
        _logger = logger;

        if (_options.Enabled && !string.IsNullOrEmpty(_options.ApiKey))
        {
            try
            {
                var builder = Kernel.CreateBuilder();

                if (_options.UseAzureOpenAI && !string.IsNullOrEmpty(_options.AzureEndpoint))
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: _options.ModelId,
                        endpoint: _options.AzureEndpoint,
                        apiKey: _options.ApiKey);
                }
                else
                {
                    builder.AddOpenAIChatCompletion(
                        modelId: _options.ModelId,
                        apiKey: _options.ApiKey);
                }

                _kernel = builder.Build();

                // Register plugins
                _kernel.Plugins.AddFromObject(new MarineDataPlugin(_context), "MarineData");
                _kernel.Plugins.AddFromObject(new SpatialQueryPlugin(_context), "SpatialQuery");

                _logger.LogInformation("MarineAI service initialized with model {Model}", _options.ModelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MarineAI service");
            }
        }
    }

    public bool IsConfigured => _kernel != null;

    /// <summary>
    /// Interpret a query before execution (US-5.1.2)
    /// "I understood this as: Reefs with DHW > 4 in Exumas. Correct?"
    /// </summary>
    public async Task<QueryInterpretation> InterpretQueryAsync(
        string naturalLanguageQuery,
        UserPersona persona = UserPersona.General,
        CancellationToken cancellationToken = default)
    {
        var interpretationId = Guid.NewGuid().ToString("N")[..12];

        // Create audit log entry
        var auditLog = NLQAuditLog.Create(naturalLanguageQuery, persona);

        // Check for security-restricted terms (US-5.1.5)
        var securityWarning = CheckSecurityRestrictions(naturalLanguageQuery, persona);
        if (securityWarning != null)
        {
            auditLog.MarkSecurityBlocked(securityWarning);
            await SaveAuditLog(auditLog, cancellationToken);

            return new QueryInterpretation(
                interpretationId,
                naturalLanguageQuery,
                "Query blocked due to security restrictions",
                Array.Empty<string>(),
                null,
                false,
                securityWarning,
                persona);
        }

        // Check for vague terms (US-5.1.4)
        var disambiguations = DetectVagueTerms(naturalLanguageQuery);

        // Determine data sources that will be used
        var dataSources = DetermineDataSources(naturalLanguageQuery);

        // Generate interpretation using AI if configured
        string interpretation;
        if (IsConfigured)
        {
            interpretation = await GenerateInterpretationAsync(naturalLanguageQuery, cancellationToken);
        }
        else
        {
            interpretation = GenerateBasicInterpretation(naturalLanguageQuery, dataSources);
        }

        auditLog.MarkInterpreted(interpretation, dataSources, disambiguations.Count > 0);
        await SaveAuditLog(auditLog, cancellationToken);

        // Cache the interpretation for later execution
        _interpretationCache[interpretationId] = new CachedInterpretation(
            naturalLanguageQuery,
            persona,
            DateTime.UtcNow.AddMinutes(10),
            auditLog.Id);

        return new QueryInterpretation(
            interpretationId,
            naturalLanguageQuery,
            interpretation,
            dataSources,
            disambiguations.Count > 0 ? disambiguations : null,
            disambiguations.Count > 0,
            null,
            persona);
    }

    /// <summary>
    /// Execute a previously interpreted query
    /// </summary>
    public async Task<MarineQueryResult> ExecuteInterpretedQueryAsync(
        string interpretationId,
        CancellationToken cancellationToken = default)
    {
        if (!_interpretationCache.TryGetValue(interpretationId, out var cached))
        {
            return new MarineQueryResult(false, Error: "Interpretation not found or expired. Please interpret the query again.");
        }

        if (cached.ExpiresAt < DateTime.UtcNow)
        {
            _interpretationCache.TryRemove(interpretationId, out _);
            return new MarineQueryResult(false, Error: "Interpretation expired. Please interpret the query again.");
        }

        // Execute the actual query
        var result = await QueryAsync(cached.Query, cached.Persona, cancellationToken);

        // Clean up cache
        _interpretationCache.TryRemove(interpretationId, out _);

        return result;
    }

    public Task<MarineQueryResult> QueryAsync(
        string naturalLanguageQuery,
        CancellationToken cancellationToken = default) =>
        QueryAsync(naturalLanguageQuery, UserPersona.General, cancellationToken);

    public async Task<MarineQueryResult> QueryAsync(
        string naturalLanguageQuery,
        UserPersona persona,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Create audit log entry
        var auditLog = NLQAuditLog.Create(naturalLanguageQuery, persona);

        // Check for security-restricted terms (US-5.1.5)
        var securityWarning = CheckSecurityRestrictions(naturalLanguageQuery, persona);
        if (securityWarning != null)
        {
            auditLog.MarkSecurityBlocked(securityWarning);
            await SaveAuditLog(auditLog, cancellationToken);
            return new MarineQueryResult(false, Error: securityWarning);
        }

        if (!IsConfigured)
        {
            auditLog.MarkFailed("AI service not configured", (int)stopwatch.ElapsedMilliseconds);
            await SaveAuditLog(auditLog, cancellationToken);
            return new MarineQueryResult(false, Error: "AI service is not configured. Please set MarineAI:ApiKey in configuration.");
        }

        try
        {
            var chatService = _kernel!.GetRequiredService<IChatCompletionService>();

            // US-5.2.4: Include query pattern context for response formatting
            var systemPrompt = GetSystemPrompt(persona, naturalLanguageQuery);
            var chatHistory = new ChatHistory(systemPrompt);
            chatHistory.AddUserMessage(naturalLanguageQuery);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = _options.MaxTokens,
                Temperature = _options.Temperature,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                _kernel,
                cancellationToken);

            stopwatch.Stop();

            // Determine what data sources were used
            var dataSources = DetermineDataSources(naturalLanguageQuery);
            var interpretation = GenerateBasicInterpretation(naturalLanguageQuery, dataSources);

            auditLog.MarkInterpreted(interpretation, dataSources);
            auditLog.MarkExecuted(null, (int)stopwatch.ElapsedMilliseconds);
            await SaveAuditLog(auditLog, cancellationToken);

            _logger.LogInformation(
                "AI query processed for persona {Persona} in {Ms}ms: {Query}",
                persona,
                stopwatch.ElapsedMilliseconds,
                naturalLanguageQuery);

            return new MarineQueryResult(
                Success: true,
                Answer: response.Content,
                Persona: persona,
                InterpretedAs: interpretation);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            auditLog.MarkFailed(ex.Message, (int)stopwatch.ElapsedMilliseconds);
            await SaveAuditLog(auditLog, cancellationToken);

            _logger.LogError(ex, "Error processing AI query for persona {Persona}: {Query}", persona, naturalLanguageQuery);
            return new MarineQueryResult(false, Error: ex.Message);
        }
    }

    public Task<IReadOnlyList<string>> GetSuggestedQueriesAsync(
        CancellationToken cancellationToken = default)
    {
        var suggestions = new List<string>
        {
            "How many Marine Protected Areas are in the Bahamas?",
            "Which MPAs have NoTake protection level?",
            "Show me the latest bleaching alerts",
            "What fishing activity has been detected in the past week?",
            "Find MPAs near Nassau (longitude -77.35, latitude 25.05)",
            "Is the location -77.5, 24.25 inside any MPA?",
            "What is the total protected marine area?",
            "Show citizen observations from the past month",
            "Which areas have high bleaching risk?",
            "List MPAs in the Exumas island group"
        };

        return Task.FromResult<IReadOnlyList<string>>(suggestions);
    }

    /// <summary>
    /// Check for security-restricted queries (US-5.1.5)
    /// </summary>
    private string? CheckSecurityRestrictions(string query, UserPersona persona)
    {
        var queryLower = query.ToLowerInvariant();

        foreach (var term in SensitiveTerms)
        {
            if (queryLower.Contains(term))
            {
                // Only Rangers can query enforcement data
                if (persona != UserPersona.Ranger)
                {
                    _logger.LogWarning(
                        "Security restriction: Non-ranger persona {Persona} attempted to query sensitive term '{Term}'",
                        persona,
                        term);
                    return $"Access to enforcement-related data is restricted. This query contains sensitive terms ('{term}') that require Ranger-level access.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Detect vague terms that need disambiguation (US-5.1.4)
    /// </summary>
    private List<DisambiguationOption> DetectVagueTerms(string query)
    {
        var found = new List<DisambiguationOption>();
        var queryLower = query.ToLowerInvariant();

        foreach (var (term, disambiguation) in VagueTerms)
        {
            if (Regex.IsMatch(queryLower, $@"\b{Regex.Escape(term)}\b"))
            {
                found.Add(disambiguation);
            }
        }

        return found;
    }

    /// <summary>
    /// Determine which data sources will be used based on query keywords
    /// </summary>
    private List<string> DetermineDataSources(string query)
    {
        var sources = new List<string>();
        var queryLower = query.ToLowerInvariant();

        if (queryLower.Contains("mpa") || queryLower.Contains("protected area") || queryLower.Contains("boundary"))
            sources.Add("Marine Protected Areas database");

        if (queryLower.Contains("bleach") || queryLower.Contains("coral") || queryLower.Contains("dhw") || queryLower.Contains("temperature"))
            sources.Add("NOAA Coral Reef Watch (bleaching alerts)");

        if (queryLower.Contains("fish") || queryLower.Contains("vessel") || queryLower.Contains("boat") || queryLower.Contains("activity"))
            sources.Add("Global Fishing Watch (vessel tracking)");

        if (queryLower.Contains("reef") || queryLower.Contains("health"))
            sources.Add("Reef health monitoring data");

        if (queryLower.Contains("observation") || queryLower.Contains("citizen") || queryLower.Contains("report"))
            sources.Add("Citizen science observations");

        if (queryLower.Contains("species") || queryLower.Contains("lionfish") || queryLower.Contains("invasive") || queryLower.Contains("endangered"))
            sources.Add("Bahamian species database");

        if (queryLower.Contains("location") || queryLower.Contains("near") || queryLower.Contains("distance") || queryLower.Contains("within"))
            sources.Add("PostGIS spatial analysis");

        if (sources.Count == 0)
            sources.Add("General marine database");

        return sources;
    }

    /// <summary>
    /// Generate basic interpretation without AI
    /// </summary>
    private string GenerateBasicInterpretation(string query, IEnumerable<string> dataSources)
    {
        var sources = string.Join(", ", dataSources);
        return $"Query: \"{query}\" - Will search: {sources}";
    }

    /// <summary>
    /// Generate AI-powered interpretation
    /// </summary>
    private async Task<string> GenerateInterpretationAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var chatService = _kernel!.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory(InterpretationPrompt);
            chatHistory.AddUserMessage(query);

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 300,
                Temperature = 0.1
            };

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                cancellationToken: cancellationToken);

            return response.Content ?? GenerateBasicInterpretation(query, DetermineDataSources(query));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI interpretation, using basic");
            return GenerateBasicInterpretation(query, DetermineDataSources(query));
        }
    }

    private async Task SaveAuditLog(NLQAuditLog auditLog, CancellationToken cancellationToken)
    {
        try
        {
            _context.NLQAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log but don't fail the main operation
            _logger.LogWarning(ex, "Failed to save NLQ audit log");
        }
    }

    private record CachedInterpretation(
        string Query,
        UserPersona Persona,
        DateTime ExpiresAt,
        Guid AuditLogId);
}
