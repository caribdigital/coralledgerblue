using System.Text.Json;
using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CoralLedger.Blue.Infrastructure.AI;

public class SpeciesClassificationService : ISpeciesClassificationService
{
    private readonly MarineAIOptions _options;
    private readonly IMarineDbContext _context;
    private readonly ILogger<SpeciesClassificationService> _logger;
    private readonly Kernel? _kernel;

    private const string ClassificationPrompt = @"
You are a marine biology expert specializing in Caribbean and Bahamian marine species identification.
Analyze the provided image and identify all marine species visible.

For each species identified, provide:
1. Scientific name (Latin binomial)
2. Common name
3. Confidence score (0-100)
4. Whether it's an invasive species (especially Lionfish - Pterois volitans/miles)
5. Conservation concern level (check for endangered/threatened species)
6. Health status if applicable (Healthy, Stressed, Bleached for corals)
7. Any relevant notes

IMPORTANT FLAGS:
- Lionfish (Pterois volitans or Pterois miles) - INVASIVE, HIGH PRIORITY for removal
- Elkhorn Coral (Acropora palmata) - CRITICALLY ENDANGERED
- Staghorn Coral (Acropora cervicornis) - CRITICALLY ENDANGERED
- Nassau Grouper (Epinephelus striatus) - CRITICALLY ENDANGERED
- Hawksbill Turtle (Eretmochelys imbricata) - CRITICALLY ENDANGERED
- Queen Conch (Strombus gigas) - VULNERABLE, protected

If confidence is below 85%, set requiresExpertVerification to true.

Return your response as valid JSON array:
[
  {
    ""scientificName"": ""Pterois volitans"",
    ""commonName"": ""Red Lionfish"",
    ""confidenceScore"": 95,
    ""requiresExpertVerification"": false,
    ""isInvasive"": true,
    ""isConservationConcern"": false,
    ""healthStatus"": null,
    ""notes"": ""Invasive species - report for removal""
  }
]

If no marine species are identifiable, return an empty array: []
";

    public SpeciesClassificationService(
        IOptions<MarineAIOptions> options,
        IMarineDbContext context,
        ILogger<SpeciesClassificationService> logger)
    {
        _options = options.Value;
        _context = context;
        _logger = logger;

        if (_options.Enabled && !string.IsNullOrEmpty(_options.ApiKey))
        {
            try
            {
                var builder = Kernel.CreateBuilder();

                // Use gpt-4o for vision capabilities (gpt-4o-mini also supports vision)
                var modelId = _options.ModelId.Contains("4o") ? _options.ModelId : "gpt-4o-mini";

                if (_options.UseAzureOpenAI && !string.IsNullOrEmpty(_options.AzureEndpoint))
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: modelId,
                        endpoint: _options.AzureEndpoint,
                        apiKey: _options.ApiKey);
                }
                else
                {
                    builder.AddOpenAIChatCompletion(
                        modelId: modelId,
                        apiKey: _options.ApiKey);
                }

                _kernel = builder.Build();
                _logger.LogInformation("Species classification service initialized with model {Model}", modelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize species classification service");
            }
        }
    }

    public bool IsConfigured => _kernel != null;

    public async Task<SpeciesClassificationResult> ClassifyPhotoAsync(
        string photoUri,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new SpeciesClassificationResult(
                false,
                Array.Empty<IdentifiedSpecies>(),
                "Classification service is not configured. Please set MarineAI:ApiKey in configuration.");
        }

        try
        {
            var chatService = _kernel!.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(ClassificationPrompt);

            // Add image and request
            chatHistory.AddUserMessage(new ChatMessageContentItemCollection
            {
                new ImageContent(new Uri(photoUri)),
                new TextContent("Please identify all marine species visible in this image.")
            });

            var settings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 1000,
                Temperature = 0.1 // Low temperature for more deterministic classification
            };

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                _kernel,
                cancellationToken).ConfigureAwait(false);

            var jsonContent = response.Content?.Trim() ?? "[]";

            // Extract JSON from markdown code blocks if present
            if (jsonContent.Contains("```"))
            {
                var startIndex = jsonContent.IndexOf('[');
                var endIndex = jsonContent.LastIndexOf(']');
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    jsonContent = jsonContent.Substring(startIndex, endIndex - startIndex + 1);
                }
            }

            var species = JsonSerializer.Deserialize<List<IdentifiedSpeciesDto>>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<IdentifiedSpeciesDto>();

            // Lookup local Bahamian names from database (Sprint 4.3 US-4.3.6)
            var scientificNames = species
                .Where(s => !string.IsNullOrEmpty(s.ScientificName))
                .Select(s => s.ScientificName!.ToLower())
                .ToList();

            var localNameLookup = await _context.BahamianSpecies
                .AsNoTracking()
                .Where(bs => scientificNames.Contains(bs.ScientificName.ToLower()))
                .ToDictionaryAsync(
                    bs => bs.ScientificName.ToLower(),
                    bs => new { bs.Id, bs.LocalName },
                    cancellationToken).ConfigureAwait(false);

            var result = species.Select(s =>
            {
                var key = (s.ScientificName ?? "").ToLower();
                localNameLookup.TryGetValue(key, out var dbInfo);

                return new IdentifiedSpecies(
                    s.ScientificName ?? "Unknown",
                    s.CommonName ?? "Unknown",
                    dbInfo?.LocalName,  // Local Bahamian name from database
                    s.ConfidenceScore,
                    s.RequiresExpertVerification || s.ConfidenceScore < 85,
                    s.IsInvasive,
                    s.IsConservationConcern,
                    s.HealthStatus,
                    s.Notes,
                    dbInfo?.Id);  // Database species ID for linking
            }).ToList();

            _logger.LogInformation(
                "Classified {Count} species from photo. Invasive: {Invasive}, Conservation concern: {Conservation}",
                result.Count,
                result.Count(s => s.IsInvasive),
                result.Count(s => s.IsConservationConcern));

            return new SpeciesClassificationResult(true, result);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse species classification response");
            return new SpeciesClassificationResult(
                false,
                Array.Empty<IdentifiedSpecies>(),
                "Failed to parse classification response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying species from photo: {Uri}", photoUri);
            return new SpeciesClassificationResult(
                false,
                Array.Empty<IdentifiedSpecies>(),
                ex.Message);
        }
    }

    private record IdentifiedSpeciesDto(
        string? ScientificName,
        string? CommonName,
        double ConfidenceScore,
        bool RequiresExpertVerification,
        bool IsInvasive,
        bool IsConservationConcern,
        string? HealthStatus,
        string? Notes);
}
