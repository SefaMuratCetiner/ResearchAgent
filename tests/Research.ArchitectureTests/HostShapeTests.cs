namespace Research.ArchitectureTests;

/// <summary>Every service entry point (.Bootstrap or .Worker) has consistent host files and keeps secrets out of appsettings.</summary>
public sealed class HostShapeTests
{
    public static TheoryData<string> EntryPoints => [.. Solution.Projects.Where(static p => p.IsEntryPoint).Select(static p => p.Name)];

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void EntryPoint_HasCompositionRootShape(string name)
    {
        var directory = Path.GetDirectoryName(Solution.Get(name).Path)!;

        File.Exists(Path.Combine(directory, "Program.cs")).ShouldBeTrue($"{name}/Program.cs");
        Directory.EnumerateFiles(Path.Combine(directory, "CompositionRoot"), "*CompositionRoot.cs").ShouldNotBeEmpty($"{name} composition root");
        Directory.EnumerateFiles(Path.Combine(directory, "CompositionRoot"), "*Settings.cs").ShouldNotBeEmpty($"{name} settings");
        Directory.EnumerateFiles(Path.Combine(directory, "Features"), "*Feature.cs").ShouldNotBeEmpty($"{name} features");
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void EntryPoint_ProgramOnlyComposes(string name)
    {
        var program = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Solution.Get(name).Path)!, "Program.cs"));

        program.ShouldContain("ComposeServices()");
        program.ShouldContain("ComposeEndpoints(app)");
        program.ShouldNotContain("builder.Services.Add", customMessage: $"{name}: registrations belong in features");
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void EntryPoint_HasAppSettingsWithServiceName(string name)
    {
        var settings = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Solution.Get(name).Path)!, "appsettings.json"));
        settings.ShouldContain($"\"ServiceName\": \"{name.Split('.')[1].ToLowerInvariant()}\"");
    }

    [Fact]
    public void NoSecretsInAppSettings()
    {
        var objSegment = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        var binSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Solution.Root, "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains(objSegment, StringComparison.OrdinalIgnoreCase) && !f.Contains(binSegment, StringComparison.OrdinalIgnoreCase)))
        {
            File.ReadAllText(file).ShouldNotContain("\"ApiKey\"", customMessage: $"{file} must not hold an API key");
        }
    }
}
