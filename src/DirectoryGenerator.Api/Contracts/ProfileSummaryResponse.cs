namespace DirectoryGenerator.Api.Contracts;

public sealed record ProfileSummaryResponse(
    string Id,
    string DisplayName,
    IReadOnlyList<string> SupportedLocales);