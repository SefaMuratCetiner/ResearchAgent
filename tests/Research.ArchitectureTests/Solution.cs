using System.Xml.Linq;

namespace Research.ArchitectureTests;

/// <summary>One project file: its name, layer and what it references.</summary>
internal sealed record ProjectInfo(
    string Name,
    string Path,
    string Sdk,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences,
    IReadOnlyList<string> FrameworkReferences)
{
    public bool IsHost => Name.EndsWith(".Host", StringComparison.Ordinal);

    public bool IsApi => Name.EndsWith(".Api", StringComparison.Ordinal);

    public bool IsPostgresAdapter => Name.EndsWith(".Postgres", StringComparison.Ordinal) && !Name.StartsWith("Research.Providers.", StringComparison.Ordinal);

    public bool IsProvider => Name.StartsWith("Research.Providers.", StringComparison.Ordinal);

    public bool IsShared => Path.Contains($"{System.IO.Path.DirectorySeparatorChar}Shared{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    public bool IsAppHost => Name == "Research.AppHost";

    public bool IsTool => Path.Contains($"{System.IO.Path.DirectorySeparatorChar}tools{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>A service module: <c>Research.&lt;Service&gt;</c> or a strategy library such as <c>Research.Search.AnthropicWebSearch</c>.</summary>
    public bool IsModule => !IsHost && !IsApi && !IsPostgresAdapter && !IsProvider && !IsShared && !IsAppHost && !IsTool;
}

/// <summary>Finds the repository root and loads every project under <c>src/</c> and <c>tools/</c>.</summary>
internal static class Solution
{
    private static readonly Lazy<IReadOnlyList<ProjectInfo>> LazyProjects = new(Load);

    public static string Root { get; } = FindRoot();

    public static IReadOnlyList<ProjectInfo> Projects => LazyProjects.Value;

    public static ProjectInfo Get(string name) => Projects.Single(p => p.Name == name);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Research.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Research.slnx not found above " + AppContext.BaseDirectory);
    }

    private static List<ProjectInfo> Load() =>
        new[] { "src", "tools" }
            .SelectMany(folder => Directory.EnumerateFiles(System.IO.Path.Combine(Root, folder), "*.csproj", SearchOption.AllDirectories))
            .Select(Read)
            .ToList();

    private static ProjectInfo Read(string path)
    {
        var document = XDocument.Load(path);
        var items = document.Descendants().ToList();
        return new ProjectInfo(
            System.IO.Path.GetFileNameWithoutExtension(path),
            path,
            document.Root?.Attribute("Sdk")?.Value ?? string.Empty,
            items.Where(static e => e.Name.LocalName == "ProjectReference")
                .Select(static e => System.IO.Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))
                .ToList(),
            items.Where(static e => e.Name.LocalName == "PackageReference").Select(static e => e.Attribute("Include")!.Value).ToList(),
            items.Where(static e => e.Name.LocalName == "FrameworkReference").Select(static e => e.Attribute("Include")!.Value).ToList());
    }
}
