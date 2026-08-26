namespace DirectoryGenerator.Api.Directory.Profiles;

public sealed record DirectoryProfile
{
    public required string Id { get; init; }

    public required IReadOnlyDictionary<string, string> DisplayNames { get; init; }

    public required string Filter { get; init; }

    public required IReadOnlyList<string> Properties { get; init; }

    public DirectorySort? Sort { get; init; }
}

public sealed record DirectorySort
{
    public DirectorySortRule? GroupBy { get; init; }

    public IReadOnlyList<DirectorySortRule> Entries { get; init; } = [];
}

public sealed record DirectorySortRule
{
    public required string Property { get; init; }

    public string Direction { get; init; } = "ascending";
}