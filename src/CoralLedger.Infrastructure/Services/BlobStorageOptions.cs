namespace CoralLedger.Infrastructure.Services;

public class BlobStorageOptions
{
    public const string SectionName = "AzureStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "observation-photos";
    public bool Enabled { get; set; } = false;
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB default
}
