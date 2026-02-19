using ObservabilityDemo.Domain.Entities;

namespace ObservabilityDemo.ArchitectureTests;

public sealed class LayeringTests
{
    [Fact]
    public void DomainAssembly_DoesNotReferenceApplicationOrInfrastructure()
    {
        var references = typeof(Tenant)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .Where(x => x is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("ObservabilityDemo.Application", references);
        Assert.DoesNotContain("ObservabilityDemo.Infrastructure", references);
    }
}
