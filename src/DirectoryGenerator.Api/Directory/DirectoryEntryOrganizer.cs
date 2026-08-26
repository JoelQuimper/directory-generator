using System.Globalization;
using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory.Profiles;

namespace DirectoryGenerator.Api.Directory;

public sealed class DirectoryEntryOrganizer : IDirectoryEntryOrganizer
{
    public IReadOnlyList<DirectoryGroupPreview> Organize(
        IReadOnlyList<DirectoryEntryPreview> entries,
        DirectorySort? sort,
        string locale)
    {
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo(locale), ignoreCase: true);
        var sortedEntries = ApplySort(entries, sort?.Entries ?? [], comparer);
        var groupRule = sort?.GroupBy;

        if (groupRule is null)
        {
            return [new DirectoryGroupPreview(null, sortedEntries.ToArray())];
        }

        var groups = sortedEntries.GroupBy(
            entry => GetPropertyValue(entry, groupRule.Property),
            comparer);
        groups = groupRule.Direction == "descending"
            ? groups.OrderByDescending(group => group.Key, comparer)
            : groups.OrderBy(group => group.Key, comparer);

        return groups
            .Select(group => new DirectoryGroupPreview(group.Key, group.ToArray()))
            .ToArray();
    }

    private static IOrderedEnumerable<DirectoryEntryPreview> ApplySort(
        IEnumerable<DirectoryEntryPreview> entries,
        IReadOnlyList<DirectorySortRule> rules,
        StringComparer comparer)
    {
        if (rules.Count == 0)
        {
            return entries.OrderBy(_ => 0);
        }

        IOrderedEnumerable<DirectoryEntryPreview>? ordered = null;
        foreach (var rule in rules)
        {
            Func<DirectoryEntryPreview, string?> selector =
                entry => GetPropertyValue(entry, rule.Property);

            ordered = ordered is null
                ? rule.Direction == "descending"
                    ? entries.OrderByDescending(selector, comparer)
                    : entries.OrderBy(selector, comparer)
                : rule.Direction == "descending"
                    ? ordered.ThenByDescending(selector, comparer)
                    : ordered.ThenBy(selector, comparer);
        }

        return ordered!;
    }

    private static string? GetPropertyValue(DirectoryEntryPreview entry, string property) =>
        property.ToLowerInvariant() switch
        {
            "id" => entry.Id,
            "displayname" => entry.DisplayName,
            "givenname" => entry.GivenName,
            "surname" => entry.Surname,
            "jobtitle" => entry.JobTitle,
            "department" => entry.Department,
            "companyname" => entry.CompanyName,
            "officelocation" => entry.OfficeLocation,
            "businessphones" => string.Join("\u001f", entry.BusinessPhones),
            "mobilephone" => entry.MobilePhone,
            "mail" => entry.Mail,
            "userprincipalname" => entry.UserPrincipalName,
            "accountenabled" => entry.AccountEnabled?.ToString(),
            "usertype" => entry.UserType,
            "onpremisesextensionattributes" => entry.ExtensionAttributes is null
                ? null
                : string.Join("\u001f", entry.ExtensionAttributes.OrderBy(item => item.Key)
                    .Select(item => item.Value)),
            _ => throw new InvalidOperationException($"Unsupported directory property '{property}'.")
        };
}