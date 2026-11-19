namespace Research.ArchitectureTests;

public sealed class ReferenceRuleTests
{
    public static TheoryData<string> Modules => [.. Solution.Projects.Where(static p => p.IsModule).Select(static p => p.Name)];

    public static TheoryData<string> Providers => [.. Solution.Projects.Where(static p => p.IsProvider).Select(static p => p.Name)];

    [Fact]
    public void ProjectsAreFound() => Solution.Projects.Count.ShouldBeGreaterThan(25);

    [Fact]
    public void SharedKernelAndContracts_HaveNoReferencesAtAll()
    {
        foreach (var name in new[] { "Research.SharedKernel", "Research.Contracts" })
        {
            var project = Solution.Get(name);
            project.PackageReferences.ShouldBeEmpty(name);
            project.FrameworkReferences.ShouldBeEmpty(name);
            if (name == "Research.Contracts")
            {
                project.ProjectReferences.ShouldBeEmpty(name);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Modules_DependOnlyOnSharedLibrariesAndTheirOwnModule(string name)
    {
        var project = Solution.Get(name);
        var service = name.Split('.')[1];

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = reference is "Research.SharedKernel" or "Research.Contracts" or "Research.Messaging"
                || reference == $"Research.{service}";
            allowed.ShouldBeTrue($"{name} must not reference {reference}");
        }

        project.FrameworkReferences.ShouldNotContain("Microsoft.AspNetCore.App", $"{name} must not depend on ASP.NET Core");
        project.PackageReferences.ShouldNotContain(static p => p.StartsWith("Aspire.", StringComparison.Ordinal), $"{name} must not use Aspire");
        project.PackageReferences.ShouldNotContain("Npgsql", $"{name} must not use Npgsql");
        project.PackageReferences.ShouldNotContain("Dapper", $"{name} must not use Dapper");
        project.PackageReferences.ShouldNotContain("RabbitMQ.Client", $"{name} must not use RabbitMQ.Client");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Providers_DoNotReferenceModulesOrHosting(string name)
    {
        foreach (var reference in Solution.Get(name).ProjectReferences)
        {
            (reference is "Research.SharedKernel" or "Research.Contracts" or "Research.Messaging")
                .ShouldBeTrue($"{name} must not reference {reference}");
        }
    }

    [Fact]
    public void OnlyHostsAndTheAppHostReferenceAspireClientIntegrations()
    {
        foreach (var project in Solution.Projects.Where(static p => !p.IsHost && !p.IsAppHost))
        {
            project.PackageReferences.ShouldNotContain(
                static p => p.StartsWith("Aspire.", StringComparison.Ordinal),
                $"{project.Name} must not reference Aspire packages");
        }
    }

    [Fact]
    public void NoProjectReferencesAHost()
    {
        foreach (var project in Solution.Projects.Where(static p => !p.IsAppHost))
        {
            project.ProjectReferences.ShouldNotContain(
                static r => r.EndsWith(".Host", StringComparison.Ordinal),
                $"{project.Name} must not reference a Host");
        }
    }

    [Fact]
    public void AppHost_ReferencesOnlyHostsAndTools()
    {
        foreach (var reference in Solution.Get("Research.AppHost").ProjectReferences)
        {
            (reference.EndsWith(".Host", StringComparison.Ordinal) || reference == "Research.DbScripts")
                .ShouldBeTrue($"AppHost must not reference {reference}");
        }
    }

    [Fact]
    public void Services_DoNotReferenceEachOther()
    {
        foreach (var project in Solution.Projects.Where(static p => p.IsHost || p.IsApi || p.IsPostgresAdapter || p.IsModule))
        {
            var service = project.Name.Split('.')[1];
            foreach (var reference in project.ProjectReferences)
            {
                var parts = reference.Split('.');
                var referencedService = parts.Length > 1 ? parts[1] : reference;
                var isServiceProject = Solution.Projects.Any(p => p.Name == reference && (p.IsModule || p.IsHost || p.IsApi || p.IsPostgresAdapter));
                if (isServiceProject)
                {
                    referencedService.ShouldBe(service, $"{project.Name} must not reference {reference} of another service");
                }
            }
        }
    }

    [Fact]
    public void ServiceDefaultsProject_DoesNotExist() =>
        Solution.Projects.ShouldNotContain(static p => p.Name.Contains("ServiceDefaults", StringComparison.Ordinal));
}
