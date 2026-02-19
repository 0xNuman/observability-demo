namespace ObservabilityDemo.Api.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }

    public bool HasTenant => TenantId != Guid.Empty;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        TenantId = tenantId;
    }
}
