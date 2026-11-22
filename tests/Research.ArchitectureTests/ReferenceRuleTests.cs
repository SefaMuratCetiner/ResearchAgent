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
        foreach (var name in new[] { "Research.Shared.Kernel", "Research.Shared.Contracts" })
        {
            var project = Solution.Get(name);
            project.PackageReferences.ShouldBeEmpty(name);
            project.FrameworkReferences.ShouldBeEmpty(name);
            project.ProjectReferences.ShouldBeEmpty(name);
        }
    }

    [Fact]
    public void SharedCoreAndPorts_DependOnlyOnSharedKernel_WithNoPackages()
    {
        foreach (var name in new[] { "Research.Shared.Core", "Research.Shared.Ports" })
        {
            var project = Solution.Get(name);
            project.PackageReferences.ShouldBeEmpty(name);
            project.FrameworkReferences.ShouldBeEmpty(name);
            foreach (var reference in project.ProjectReferences)
            {
                reference.ShouldBe("Research.Shared.Kernel", $"{name} must not reference {reference}");
            }
        }
    }

    [Fact]
    public void SharedMessaging_DependsOnlyOnSharedKernelSharedCoreAndContracts_WithNoPackages()
    {
        var project = Solution.Get("Research.Shared.Messaging");
        project.PackageReferences.ShouldBeEmpty();
        project.FrameworkReferences.ShouldBeEmpty();
        foreach (var reference in project.ProjectReferences)
        {
            (reference is "Research.Shared.Kernel" or "Research.Shared.Core" or "Research.Shared.Contracts")
                .ShouldBeTrue($"Research.Shared.Messaging must not reference {reference}");
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
            reference.ShouldBe("Research.Shared.Kernel", $"{name} must not reference {reference}");
        }
    }

    [Theory]
    [MemberData(nameof(Applications))]
    public void Application_DependsOnlyOnOwnDomainSharedAbstractionsAndContracts(string name)
    {
        var project = Solution.Get(name);
        var ownDomain = $"Research.{project.ServiceName}.Domain";

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = reference is "Research.Shared.Core" or "Research.Shared.Messaging" or "Research.Shared.Ports" or "Research.Shared.Contracts"
                || string.Equals(reference, ownDomain, StringComparison.Ordinal);
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
            var allowed = string.Equals(reference, ownApp, StringComparison.Ordinal)
                || string.Equals(reference, "Research.Shared.Adapters", StringComparison.Ordinal)
                || reference.StartsWith("Research.Providers.", StringComparison.Ordinal);
            allowed.ShouldBeTrue($"{name} must not reference {reference}");
        }
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void Api_DependsOnlyOnOwnApplicationAndSharedWeb(string name)
    {
        var project = Solution.Get(name);
        var ownApp = $"Research.{project.ServiceName}.Application";

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = string.Equals(reference, ownApp, StringComparison.Ordinal)
                || string.Equals(reference, "Research.Shared.Web", StringComparison.Ordinal);
            allowed.ShouldBeTrue($"{name} must not reference {reference}");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Providers_DependOnlyOnSharedAbstractionsSharedAdaptersAndContracts(string name)
    {
        foreach (var reference in Solution.Get(name).ProjectReferences)
        {
            (reference is "Research.Shared.Core" or "Research.Shared.Messaging" or "Research.Shared.Ports" or "Research.Shared.Adapters" or "Research.Shared.Contracts")
                .ShouldBeTrue($"{name} must not reference {reference}");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    [MemberData(nameof(Applications))]
    [MemberData(nameof(Infrastructures))]
    public void ProvidersAndServiceLayers_OwnTheirServiceCollectionExtensions(string name)
    {
        var directory = Path.GetDirectoryName(Solution.Get(name).Path)!;
        Directory.EnumerateFiles(directory, "*ServiceCollectionExtensions.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ShouldNotBeEmpty($"{name} should own its DI registration via a *ServiceCollectionExtensions class");
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
                var target = Solution.Projects.FirstOrDefault(p => string.Equals(p.Name, reference, StringComparison.Ordinal));
                if (target is { IsServiceProject: true })
                {
                    target.ServiceName.ShouldBe(project.ServiceName, $"{project.Name} must not reference {reference} of another service");
                }
            }
        }
    }

    [Fact]
    public void ServiceDefaultsProject_ExistsHasNoProjectReferencesAndIsOnlyReferencedByEntryPoints()
    {
        var serviceDefaults = Solution.Get("Research.ServiceDefaults");
        serviceDefaults.ProjectReferences.ShouldBeEmpty("Research.ServiceDefaults must remain strictly Aspire defaults with no project references");

        foreach (var project in Solution.Projects.Where(static p => !p.IsEntryPoint))
        {
            project.ProjectReferences.ShouldNotContain("Research.ServiceDefaults", $"{project.Name} must not reference Research.ServiceDefaults");
        }
    }

    [Fact]
    public void SharedWebProject_ExistsAndIsOnlyReferencedByHttpHostsAndApis()
    {
        var sharedWeb = Solution.Get("Research.Shared.Web");
        foreach (var reference in sharedWeb.ProjectReferences)
        {
            (reference is "Research.Shared.Kernel" or "Research.Shared.Core")
                .ShouldBeTrue($"Research.Shared.Web must not reference {reference}");
        }

        foreach (var project in Solution.Projects.Where(static p => !p.IsHttpHost && !p.IsApi))
        {
            project.ProjectReferences.ShouldNotContain("Research.Shared.Web", $"{project.Name} must not reference Research.Shared.Web");
        }
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void Api_ExposesAnIEndpointGroupAndEndpoints(string name)
    {
        var directory = Path.GetDirectoryName(Solution.Get(name).Path)!;
        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .ToList();

        files.Any(static content => content.Contains(": IEndpointGroup", StringComparison.Ordinal))
            .ShouldBeTrue($"{name} must define at least one IEndpointGroup");
        files.Any(static content => content.Contains(": IEndpoint", StringComparison.Ordinal) && !content.Contains(": IEndpointGroup", StringComparison.Ordinal))
            .ShouldBeTrue($"{name} must define at least one REPR IEndpoint");
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void Api_DoesNotImportDomainNamespace(string name)
    {
        var project = Solution.Get(name);
        var domainNamespace = $"Research.{project.ServiceName}.Domain";
        var directory = Path.GetDirectoryName(project.Path)!;

        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            File.ReadAllText(file).ShouldNotContain($"using {domainNamespace}", customMessage: $"{file} must depend on Application DTOs rather than {domainNamespace}");
        }
    }

    [Fact]
    public void DomainUnitTests_DependOnlyOnDomainAndSharedKernel()
    {
        var project = Solution.GetTest("Research.Domain.UnitTests");
        project.FrameworkReferences.ShouldNotContain("Microsoft.AspNetCore.App", "Domain tests must not depend on ASP.NET Core");

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = string.Equals(reference, "Research.Shared.Kernel", StringComparison.Ordinal)
                || reference.EndsWith(".Domain", StringComparison.Ordinal);
            allowed.ShouldBeTrue($"Research.Domain.UnitTests must not reference {reference}");
        }
    }

    [Fact]
    public void ApplicationUnitTests_DependOnlyOnInnerLayers()
    {
        var project = Solution.GetTest("Research.Application.UnitTests");

        foreach (var reference in project.ProjectReferences)
        {
            var allowed = reference is "Research.Shared.Kernel" or "Research.Shared.Core" or "Research.Shared.Messaging" or "Research.Shared.Ports" or "Research.Shared.Contracts"
                || reference.EndsWith(".Domain", StringComparison.Ordinal)
                || reference.EndsWith(".Application", StringComparison.Ordinal);
            allowed.ShouldBeTrue($"Research.Application.UnitTests must not reference outer-layer project {reference}");
        }
    }
}
