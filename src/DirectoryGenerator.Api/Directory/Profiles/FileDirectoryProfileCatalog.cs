using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DirectoryGenerator.Api.Directory.Profiles;

public sealed partial class FileDirectoryProfileCatalog : IDirectoryProfileCatalog
{
    private static readonly HashSet<string> SupportedLocales =
        new(StringComparer.OrdinalIgnoreCase) { "en-CA", "fr-CA" };

    private static readonly HashSet<string> SupportedProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "id",
            "displayName",
            "givenName",
            "surname",
            "jobTitle",
            "department",
            "companyName",
            "officeLocation",
            "businessPhones",
            "mobilePhone",
            "mail",
            "userPrincipalName",
            "accountEnabled",
            "userType",
            "onPremisesExtensionAttributes"
        };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IReadOnlyDictionary<string, DirectoryProfile> profilesById;

    public FileDirectoryProfileCatalog(string profileDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDirectory);

        if (!System.IO.Directory.Exists(profileDirectory))
        {
            throw new InvalidDataException($"Directory profile folder '{profileDirectory}' does not exist.");
        }

        var profiles = System.IO.Directory
            .EnumerateFiles(profileDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadProfile)
            .ToArray();

        if (profiles.Length == 0)
        {
            throw new InvalidDataException($"Directory profile folder '{profileDirectory}' contains no JSON profiles.");
        }

        var duplicateId = profiles
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateId is not null)
        {
            throw new InvalidDataException($"Directory profile ID '{duplicateId}' is duplicated.");
        }

        Profiles = profiles;
        profilesById = profiles.ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<DirectoryProfile> Profiles { get; }

    public DirectoryProfile? Find(string id) =>
        profilesById.GetValueOrDefault(id);

    private static DirectoryProfile LoadProfile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var profile = JsonSerializer.Deserialize<DirectoryProfile>(stream, SerializerOptions)
                ?? throw new InvalidDataException("The profile is empty.");

            Validate(profile);
            return profile;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            throw new InvalidDataException(
                $"Directory profile '{Path.GetFileName(path)}' is invalid: {exception.Message}",
                exception);
        }
    }

    private static void Validate(DirectoryProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id) || !ProfileIdPattern().IsMatch(profile.Id))
        {
            throw new InvalidDataException("The profile ID must be URL-safe lowercase text separated by hyphens.");
        }

        if (profile.DisplayNames.Count == 0 ||
            profile.DisplayNames.Any(item =>
                !SupportedLocales.Contains(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
        {
            throw new InvalidDataException("Display names must use supported locales and non-empty values.");
        }

        ValidateFilter(profile.Filter);

        if (profile.Properties.Count == 0)
        {
            throw new InvalidDataException("At least one Graph property is required.");
        }

        ValidateProperties(profile.Properties, "properties");

        if (profile.Sort?.GroupBy is not null)
        {
            ValidateSortRule(profile.Sort.GroupBy);
        }

        foreach (var rule in profile.Sort?.Entries ?? [])
        {
            ValidateSortRule(rule);
        }
    }

    private static void ValidateFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter) ||
            filter.Any(char.IsControl) ||
            Uri.TryCreate(filter, UriKind.Absolute, out _) ||
            filter.Contains('?', StringComparison.Ordinal) ||
            filter.Contains("$select", StringComparison.OrdinalIgnoreCase) ||
            filter.Contains("$expand", StringComparison.OrdinalIgnoreCase) ||
            filter.Contains("$top", StringComparison.OrdinalIgnoreCase) ||
            filter.Contains("$skip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The Graph filter must be a non-empty OData expression without URLs or query parameters.");
        }
    }

    private static void ValidateProperties(IEnumerable<string> properties, string fieldName)
    {
        var values = properties.ToArray();
        if (values.Any(property => !SupportedProperties.Contains(property)))
        {
            throw new InvalidDataException($"The profile contains an unsupported Graph property in '{fieldName}'.");
        }

        if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
        {
            throw new InvalidDataException($"The profile contains duplicate Graph properties in '{fieldName}'.");
        }
    }

    private static void ValidateSortRule(DirectorySortRule rule)
    {
        ValidateProperties([rule.Property], "sort");

        if (rule.Direction is not ("ascending" or "descending"))
        {
            throw new InvalidDataException("Sort direction must be 'ascending' or 'descending'.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileIdPattern();
}