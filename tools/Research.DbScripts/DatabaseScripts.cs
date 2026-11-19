namespace Research.DbScripts;

/// <summary>Which script folders belong to which database (connection name).</summary>
internal sealed record DatabaseScripts(string ConnectionName, IReadOnlyList<string> Folders)
{
    /// <summary>
    /// Every database. <c>shared</c> holds <c>0001_outbox_inbox.sql</c>, which every database that owns an outbox needs;
    /// the numbering keeps one global order across folders.
    /// </summary>
    public static IReadOnlyList<DatabaseScripts> All { get; } =
    [
        new("jobs", ["shared", "jobs"]),
        new("fetching", ["shared", "fetching"]),
        new("knowledge", ["shared", "knowledge"]),
    ];
}

/// <summary>A script on disk.</summary>
internal sealed record ScriptFile(string Name, string Path);
