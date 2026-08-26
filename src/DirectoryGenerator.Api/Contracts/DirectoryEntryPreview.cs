namespace DirectoryGenerator.Api.Contracts;

public sealed record DirectoryGroupPreview(
    string? Group,
    IReadOnlyList<DirectoryEntryPreview> Entries);

public sealed record DirectoryEntryPreview(
    string? Id,
    string? DisplayName,
    string? GivenName,
    string? Surname,
    string? JobTitle,
    string? Department,
    string? CompanyName,
    string? OfficeLocation,
    IReadOnlyList<string> BusinessPhones,
    string? MobilePhone,
    string? Mail,
    string? UserPrincipalName,
    bool? AccountEnabled,
    string? UserType,
    IReadOnlyDictionary<string, string?>? ExtensionAttributes);