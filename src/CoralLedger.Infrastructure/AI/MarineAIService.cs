using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Enums;
using CoralLedger.Infrastructure.AI.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CoralLedger.Infrastructure.AI;

public class MarineAIService : IMarineAIService
{
    private readonly MarineAIOptions _options;
    private readonly IMarineDbContext _context;
    private readonly ILogger<MarineAIService> _logger;
    private readonly Kernel? _kernel;

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

    private static readonly Dictionary<UserPersona, string> PersonaPrompts = new()
    {
        [UserPersona.General] = "",
        [UserPersona.Ranger] = @"

USER PERSONA: PARK RANGER
You are speaking to a field enforcement officer. Adapt your responses to:
- Prioritize enforcement and legal compliance information
- Highlight unauthorized activities, violations, and boundary breaches
- Provide clear coordinates and locations for patrol planning
- Include actionable field intelligence with specific vessel identifications
- Flag any NoTake zone violations or suspicious activities
- Format response with ACTION ITEMS when applicable
- Keep language direct and operational",

        [UserPersona.Fisherman] = @"

USER PERSONA: FISHERMAN
You are speaking to a commercial fisherman. Adapt your responses to:
- Focus on fishing activity, sustainable catch areas, and gear regulations
- Use plain language - avoid technical jargon
- Explain how conditions affect fishing (e.g., 'waters are too warm for fish to feed' not 'SST anomaly +2.1°C')
- Highlight protected zones where fishing is restricted
- Provide practical information about seasons and quotas
- Be respectful of traditional fishing knowledge
- Include Bahamian local names for species when available",

        [UserPersona.Scientist] = @"

USER PERSONA: SCIENTIST/RESEARCHER
You are speaking to a marine researcher. Adapt your responses to:
- Include data sources and methodology notes (NOAA, Global Fishing Watch, etc.)
- Provide statistical context: sample sizes, confidence levels, temporal ranges
- Use precise scientific terminology and species names (scientific names)
- Note data limitations and uncertainty ranges
- Reference IUCN conservation status classifications
- Include DHW (Degree Heating Weeks), SST values, and other quantitative metrics
- Mention spatial analysis methods used (PostGIS functions, coordinate systems)",

        [UserPersona.Policymaker] = @"

USER PERSONA: POLICYMAKER
You are speaking to a government official or policy advisor. Adapt your responses to:
- Lead with executive summary of key findings
- Frame information in terms of policy implications and outcomes
- Highlight trends and strategic patterns
- Provide quantitative impact metrics and comparisons
- Include recommendations for regulatory or conservation actions
- Focus on ecosystem health and economic implications
- Connect findings to Bahamas marine protection goals and international commitments"
    };

    private string GetSystemPrompt(UserPersona persona) =>
        BaseSystemPrompt + PersonaPrompts.GetValueOrDefault(persona, "");

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

    public Task<MarineQueryResult> QueryAsync(
        string naturalLanguageQuery,
        CancellationToken cancellationToken = default) =>
        QueryAsync(naturalLanguageQuery, UserPersona.General, cancellationToken);

    public async Task<MarineQueryResult> QueryAsync(
        string naturalLanguageQuery,
        UserPersona persona,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new MarineQueryResult(false, Error: "AI service is not configured. Please set MarineAI:ApiKey in configuration.");
        }

        try
        {
            var chatService = _kernel!.GetRequiredService<IChatCompletionService>();

            var systemPrompt = GetSystemPrompt(persona);
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

            _logger.LogInformation("AI query processed for persona {Persona}: {Query}", persona, naturalLanguageQuery);

            return new MarineQueryResult(
                Success: true,
                Answer: response.Content,
                Persona: persona);
        }
        catch (Exception ex)
        {
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
}
