namespace CoralLedger.Blue.Domain.Common;

/// <summary>
/// Marks an entity as tenant-aware for multi-tenant isolation
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
