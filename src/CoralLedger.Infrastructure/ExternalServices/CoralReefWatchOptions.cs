namespace CoralLedger.Infrastructure.ExternalServices;

public class CoralReefWatchOptions
{
    public const string SectionName = "CoralReefWatch";

    /// <summary>
    /// When true, Coral Reef Watch requests use the local mock dataset.
    /// </summary>
    public bool UseMockData { get; set; }

    /// <summary>
    /// Relative path (from the output folder) to the mock bleaching JSON.
    /// </summary>
    public string MockDataPath { get; set; } = "data/mock-bleaching-data.json";
}
