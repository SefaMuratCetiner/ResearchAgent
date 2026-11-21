namespace Research.ArchitectureTests;

/// <summary>Enforces the Clean Architecture Dependency Rule across all 6 microservices, shared libraries and providers.</summary>
public sealed class ReferenceRuleTests
{
    public static TheoryData<string> Domains => [.. Solution.Projects.Where(static p => p.IsDomain).Select(static p => p.Name)];

    public static TheoryData<string> Applications => [.. Solution.Projects.Where(static p => p.IsApplication).Select(static p => p.Name)];

    public static TheoryData<string> Infrastructures => [.. Solution.Projects.Where(static p => p.IsInfrastructure).Select(static p => p.Name)];

    public static TheoryData<string> Apis => [.. Solution.Projects.Where(static p => p.IsApi).Select(static p => p.Name)];

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
            project.ProjectReferences.ShouldBeEmpty(name);
        }
    }

    [Fact]
    public void SharedCore_DependsOnlyOnSharedKernelAndContracts_WithNoPackages()
    {
        var project = Solution.Get("Research.Shared.Core");
        project.PackageReferences.ShouldBeEmpty();
        project.FrameworkReferences.ShouldBeEmpty();
        foreach (var reference in project.ProjectReferences)
        {
            (reference is "Research.SharedKernel" or "Research.Contracts")
                .ShouldBeTrue($"Research.Shared.Core must not reference {reference}");
        }
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Domain_DependsOnlyOnSharedKernel_WithNoPackages(string name)
    {
        var project = Solution.Get(name);
        project.PackageReferences.ShouldBeEmpty($"{name} must have zero package references");
        project.FrameworkReferences.ShouldBeEmpty($"{name} must have zero framework references");
        foreach (var reference in project.ProjectReferences)
        {
            reference.ShouldBe("Research.SharedKernel", $"{name} must not reference {reference}");
        }
    }

    [Theory]
    [MemberData(nameof(Applications))]
    public void Application_DependsOnlyOnOwnDomainSharedCoreAndContracts(string name)
    {
        var project = Solution.Get(name);
        var ownDomain = $"Research.{project.ServiceName}.Domain";

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = reference is "Research.Shared.Core" or "Research.Contracts"
                || reference == ownDomain;
            allowed.ShouldBeTrue($"{name} must not reference {reference}");
        }

        project.FrameworkReferences.ShouldNotContain("Microsoft.AspNetCore.App", $"{name} must not depend on ASP.NET Core");
        project.PackageReferences.ShouldNotContain(static p => p.StartsWith("Aspire.", StringComparison.Ordinal), $"{name} must not use Aspire");
        project.PackageReferences.ShouldNotContain("AngleSharp", $"{name} must not use AngleSharp");
        project.PackageReferences.ShouldNotContain("Microsoft.Extensions.Http", $"{name} must not use HttpClient");
        project.PackageReferences.ShouldNotContain("Npgsql", $"{name} must not use Npgsql");
        project.PackageReferences.ShouldNotContain("Dapper", $"{name} must not use Dapper");
        project.PackageReferences.ShouldNotContain("RabbitMQ.Client", $"{name} must not use RabbitMQ.Client");
    }

    [Theory]
    [MemberData(nameof(Infrastructures))]
    public void Infrastructure_DependsOnlyOnOwnApplicationSharedAdaptersAndProviders(string name)
    {
        var project = Solution.Get(name);
        var ownApp = $"Research.{project.ServiceName}.Application";

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = reference == ownApp
                || reference == "Research.Shared.Adapters"
                || reference.StartsWith("Research.Providers.", StringComparison.Ordinal);
            allowed.ShouldBeTrue($"{name} must not reference {reference}");
        }
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void Api_DependsOnlyOnOwnApplicationAndServiceDefaults(string name)
    {
        var project = Solution.Get(name);
        var ownApp = $"Research.{project.ServiceName}.Application";

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = reference == ownApp || reference == "Research.ServiceDefaults";
            allowed.ShouldBeTrue($"{name} must not reference {reference}");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Providers_DependOnlyOnSharedCoreSharedAdaptersAndContracts(string name)
    {
        foreach (var reference in Solution.Get(name).ProjectReferences)
        {
            (reference is "Research.Shared.Core" or "Research.Shared.Adapters" or "Research.Contracts")
                .ShouldBeTrue($"{name} must not reference {reference}");
        }
    }

    [Fact]
    public void OnlyEntryPointsAndAppHostReferenceAspireClientIntegrations()
    {
        foreach (var project in Solution.Projects.Where(static p => !p.IsEntryPoint && !p.IsAppHost))
        {
            project.PackageReferences.ShouldNotContain(
                static p => p.StartsWith("Aspire.", StringComparison.Ordinal),
                $"{project.Name} must not reference Aspire packages");
        }
    }

    [Fact]
    public void NoProjectReferencesAnEntryPointExceptAppHost()
    {
        foreach (var project in Solution.Projects.Where(static p => !p.IsAppHost))
        {
            project.ProjectReferences.ShouldNotContain(
                static r => r.EndsWith(".Bootstrap", StringComparison.Ordinal) || r.EndsWith(".Worker", StringComparison.Ordinal),
                $"{project.Name} must not reference a Bootstrap or Worker entry point");
        }
    }

    [Fact]
    public void AppHost_ReferencesOnlyEntryPointsAndTools()
    {
        foreach (var reference in Solution.Get("Research.AppHost").ProjectReferences)
        {
            var target = Solution.Get(reference);
            (target.IsEntryPoint || target.IsTool)
                .ShouldBeTrue($"AppHost must not reference {reference}");
        }
    }

    [Fact]
    public void Services_DoNotReferenceEachOther()
    {
        foreach (var project in Solution.Projects.Where(static p => p.IsServiceProject))
        {
            foreach (var reference in project.ProjectReferences)
            {
                var target = Solution.Projects.FirstOrDefault(p => p.Name == reference);
                if (target is { IsServiceProject: true })
                {
                    target.ServiceName.ShouldBe(project.ServiceName, $"{project.Name} must not reference {reference} of another service");
                }
            }
        }
    }

    [Fact]
    public void ServiceDefaultsProject_ExistsAndIsOnlyReferencedByEntryPointsAndApis()
    {
        Solution.Projects.ShouldContain(static p => p.IsServiceDefaults);
        foreach (var project in Solution.Projects.Where(static p => !p.IsEntryPoint && !p.IsApi))
        {
            project.ProjectReferences.ShouldNotContain("Research.ServiceDefaults", $"{project.Name} must not reference Research.ServiceDefaults");
        }
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void Api_ExposesAnIEndpointGroup(string name)
    {
        var directory = Path.GetDirectoryName(Solution.Get(name).Path)!;
        var hasGroup = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Any(static f => File.ReadAllText(f).Contains(": IEndpointGroup", StringComparison.Ordinal));

        hasGroup.ShouldBeTrue($"{name} must define at least one IEndpointGroup");
    }

    [Fact]
    public void DomainUnitTests_DependOnlyOnDomainAndSharedKernel()
    {
        var project = Solution.GetTest("Research.Domain.UnitTests");
        project.FrameworkReferences.ShouldNotContain("Microsoft.AspNetCore.App", "Domain tests must not depend on ASP.NET Core");

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = reference == "Research.SharedKernel" || reference.EndsWith(".Domain", StringComparison.Ordinal);
            allowed.ShouldBeTrue($"Research.Domain.UnitTests must not reference {reference}");
        }
    }

    [Fact]
    public void ApplicationUnitTests_DependOnlyOnInnerLayers()
    {
        var project = Solution.GetTest("Research.Application.UnitTests");

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = reference is "Research.SharedKernel" or "Research.Shared.Core" or "Research.Contracts"
                || reference.EndsWith(".Domain", StringComparison.Ordinal)
                || reference.EndsWith(".Application", StringComparison.Ordinal);
            allowed.ShouldBeTrue($"Research.Application.UnitTests must not reference outer-layer project {reference}");
        }
    }
}
