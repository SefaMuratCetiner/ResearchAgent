namespace Research.ArchitectureTests;

/// <summary>Every Host has the composition-root shape: Program, CompositionRoot (root + settings), Features.</summary>
public sealed class HostShapeTests
{
    public static TheoryData<string> Hosts => [.. Solution.Projects.Where(static p => p.IsHost).Select(static p => p.Name)];

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Host_HasCompositionRootShape(string name)
    {
        var directory = Path.GetDirectoryName(Solution.Get(name).Path)!;
        var service = name.Split('.')[1];

        File.Exists(Path.Combine(directory, "Program.cs")).ShouldBeTrue($"{name}/Program.cs");
        File.Exists(Path.Combine(directory, "CompositionRoot", $"{service}CompositionRoot.cs")).ShouldBeTrue($"{name} composition root");
        File.Exists(Path.Combine(directory, "CompositionRoot", $"{service}Settings.cs")).ShouldBeTrue($"{name} settings");
        Directory.EnumerateFiles(Path.Combine(directory, "Features"), "*Feature.cs").ShouldNotBeEmpty($"{name} features");
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Host_ProgramOnlyComposes(string name)
    {
        var program = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Solution.Get(name).Path)!, "Program.cs"));

        program.ShouldContain("ComposeServices()");
        program.ShouldContain("ComposeEndpoints(app)");
        program.ShouldNotContain("builder.Services.Add", customMessage: $"{name}: registrations belong in features");
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Host_HasAppSettingsWithServiceName(string name)
    {
        var settings = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Solution.Get(name).Path)!, "appsettings.json"));
        settings.ShouldContain($"\"ServiceName\": \"{name.Split('.')[1].ToLowerInvariant()}\"");
    }

    [Fact]
    public void NoSecretsInAppSettings()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Solution.Root, "src"), "appsettings*.json", SearchOption.AllDirectories))
        {
            File.ReadAllText(file).ShouldNotContain("\"ApiKey\"", customMessage: $"{file} must not hold an API key");
        }
    }
}
