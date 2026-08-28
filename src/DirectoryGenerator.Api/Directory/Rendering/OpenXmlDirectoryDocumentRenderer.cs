using System.Globalization;
using System.Text.RegularExpressions;
using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory.Templates;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DirectoryGenerator.Api.Directory.Rendering;

public sealed partial class OpenXmlDirectoryDocumentRenderer(
    IDirectoryTemplateLoader templateLoader) : IDirectoryDocumentRenderer
{
    private static readonly HashSet<string> SupportedTokens = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "documentTitle",
        "documentDescription",
        "generatedAt",
        "entryCount",
        "group",
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
        "extensionAttribute1",
        "extensionAttribute2",
        "extensionAttribute3",
        "extensionAttribute4",
        "extensionAttribute5",
        "extensionAttribute6",
        "extensionAttribute7",
        "extensionAttribute8",
        "extensionAttribute9",
        "extensionAttribute10",
        "extensionAttribute11",
        "extensionAttribute12",
        "extensionAttribute13",
        "extensionAttribute14",
        "extensionAttribute15"
    };

    public Stream Render(
        DirectoryDocumentContent content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        using var template = templateLoader.Open(content.ProfileId, content.Locale);
        var output = new MemoryStream();
        template.CopyTo(output);
        output.Position = 0;

        try
        {
            using (var document = WordprocessingDocument.Open(output, true))
            {
                var mainDocument = document.MainDocumentPart?.Document
                    ?? throw new InvalidDataException("The Word template has no main document.");
                var roots = new List<OpenXmlPartRootElement> { mainDocument };
                roots.AddRange(document.MainDocumentPart!.FooterParts.Select(part => part.Footer));

                ValidateTokenCatalog(roots);
                RenderGroups(mainDocument, content.Groups, cancellationToken);

                var culture = CultureInfo.GetCultureInfo(content.Locale);
                var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["documentTitle"] = content.Title,
                    ["documentDescription"] = content.Description,
                    ["generatedAt"] = content.GeneratedAt.ToString("g", culture),
                    ["entryCount"] = content.Groups.Sum(group => group.Entries.Count)
                        .ToString(culture)
                };

                foreach (var root in roots)
                {
                    ReplaceTokens(root, metadata);
                    EnsureNoTokensRemain(root);
                    root.Save();
                }
            }

            output.Position = 0;
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static void RenderGroups(
        Document document,
        IReadOnlyList<DirectoryGroupPreview> groups,
        CancellationToken cancellationToken)
    {
        var groupMarker = FindSingleMarker(document, "dg:groups");
        var outerTable = groupMarker.Descendants<Table>().FirstOrDefault()
            ?? throw new InvalidDataException(
                "The 'dg:groups' content control must contain the two-column table.");
        var outerRow = outerTable.Elements<TableRow>().SingleOrDefault()
            ?? throw new InvalidDataException(
                "The two-column directory table must contain exactly one row.");
        var outerCells = outerRow.Elements<TableCell>().ToArray();
        if (outerCells.Length != 2)
        {
            throw new InvalidDataException(
                "The two-column directory table must contain exactly two cells.");
        }

        var groupPrototype = outerCells[0].Elements<Table>().SingleOrDefault()
            ?? throw new InvalidDataException(
                "The left directory column must contain exactly one prototype group table.");
        if (outerCells[1].Elements<Table>().Any())
        {
            throw new InvalidDataException(
                "The right directory column must be empty in the template.");
        }

        var prototype = (Table)groupPrototype.CloneNode(true);
        ClearCell(outerCells[0]);
        ClearCell(outerCells[1]);

        var leftGroupCount = GetLeftGroupCount(groups);
        for (var index = 0; index < groups.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var renderedGroup = RenderGroup(prototype, groups[index], cancellationToken);
            var targetCell = index < leftGroupCount ? outerCells[0] : outerCells[1];
            targetCell.Append(renderedGroup);
        }

        outerCells[0].Append(new Paragraph());
        outerCells[1].Append(new Paragraph());
        Unwrap(groupMarker);
    }

    private static Table RenderGroup(
        Table prototype,
        DirectoryGroupPreview group,
        CancellationToken cancellationToken)
    {
        var table = (Table)prototype.CloneNode(true);
        var entryMarker = FindSingleMarker(table, "dg:entries");
        var entryPrototype = entryMarker.Descendants<TableRow>().SingleOrDefault()
            ?? throw new InvalidDataException(
                "The 'dg:entries' content control must contain exactly one prototype row.");
        var groupHeading = table.Elements<TableRow>().FirstOrDefault()
            ?? throw new InvalidDataException("The prototype group has no heading row.");

        if (string.IsNullOrWhiteSpace(group.Group))
        {
            groupHeading.Remove();
        }
        else
        {
            ReplaceTokens(
                groupHeading,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["group"] = group.Group
                });
        }

        foreach (var entry in group.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = (TableRow)entryPrototype.CloneNode(true);
            ReplaceTokens(row, CreateEntryValues(entry));
            entryMarker.InsertBeforeSelf(row);
        }

        entryMarker.Remove();
        return table;
    }

    private static IReadOnlyDictionary<string, string?> CreateEntryValues(
        DirectoryEntryPreview entry)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = entry.Id,
            ["displayName"] = entry.DisplayName,
            ["givenName"] = entry.GivenName,
            ["surname"] = entry.Surname,
            ["jobTitle"] = entry.JobTitle,
            ["department"] = entry.Department,
            ["companyName"] = entry.CompanyName,
            ["officeLocation"] = entry.OfficeLocation,
            ["businessPhones"] = string.Join('\n', entry.BusinessPhones),
            ["mobilePhone"] = entry.MobilePhone,
            ["mail"] = entry.Mail ?? entry.UserPrincipalName,
            ["userPrincipalName"] = entry.UserPrincipalName,
            ["accountEnabled"] = entry.AccountEnabled?.ToString(),
            ["userType"] = entry.UserType
        };

        for (var index = 1; index <= 15; index++)
        {
            var name = $"extensionAttribute{index}";
            values[name] = entry.ExtensionAttributes?.FirstOrDefault(attribute =>
                string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
        }

        return values;
    }

    private static int GetLeftGroupCount(IReadOnlyList<DirectoryGroupPreview> groups)
    {
        if (groups.Count <= 1)
        {
            return groups.Count;
        }

        var totalWeight = groups.Sum(GetGroupWeight);
        var cumulativeWeight = 0;
        var bestCount = 1;
        var smallestDifference = int.MaxValue;

        for (var index = 0; index < groups.Count - 1; index++)
        {
            cumulativeWeight += GetGroupWeight(groups[index]);
            var difference = Math.Abs(totalWeight - (2 * cumulativeWeight));
            if (difference < smallestDifference)
            {
                smallestDifference = difference;
                bestCount = index + 1;
            }
        }

        return bestCount;
    }

    private static int GetGroupWeight(DirectoryGroupPreview group) =>
        group.Entries.Count + (string.IsNullOrWhiteSpace(group.Group) ? 0 : 1);

    private static void ClearCell(TableCell cell)
    {
        foreach (var child in cell.ChildElements
                     .Where(child => child is not TableCellProperties)
                     .ToArray())
        {
            child.Remove();
        }
    }

    private static SdtElement FindSingleMarker(OpenXmlElement root, string tag)
    {
        var markers = root.Descendants<SdtElement>()
            .Where(marker => string.Equals(
                marker.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value,
                tag,
                StringComparison.Ordinal))
            .ToArray();

        return markers.Length == 1
            ? markers[0]
            : throw new InvalidDataException(
                $"The Word template must contain exactly one '{tag}' content control.");
    }

    private static void Unwrap(SdtElement marker)
    {
        var content = marker.ChildElements
            .FirstOrDefault(child => child.LocalName == "sdtContent")
            ?? throw new InvalidDataException("A template content control has no content.");

        foreach (var child in content.ChildElements.ToArray())
        {
            child.Remove();
            marker.InsertBeforeSelf(child);
        }

        marker.Remove();
    }

    private static void ValidateTokenCatalog(IEnumerable<OpenXmlElement> roots)
    {
        var unknownTokens = roots
            .SelectMany(root => root.Descendants<Paragraph>())
            .SelectMany(paragraph => TokenPattern().Matches(GetText(paragraph)).Cast<Match>())
            .Select(match => match.Groups[1].Value)
            .Where(token => !SupportedTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unknownTokens.Length > 0)
        {
            throw new InvalidDataException(
                $"The Word template contains unsupported tokens: {string.Join(", ", unknownTokens)}.");
        }
    }

    private static void ReplaceTokens(
        OpenXmlElement root,
        IReadOnlyDictionary<string, string?> values)
    {
        foreach (var paragraph in root.Descendants<Paragraph>().ToArray())
        {
            var originalText = GetText(paragraph);
            if (!TokenPattern().IsMatch(originalText))
            {
                continue;
            }

            var replacedText = TokenPattern().Replace(originalText, match =>
                values.TryGetValue(match.Groups[1].Value, out var value)
                    ? value ?? string.Empty
                    : match.Value);
            if (string.Equals(originalText, replacedText, StringComparison.Ordinal))
            {
                continue;
            }

            var runProperties = paragraph.Descendants<Run>().FirstOrDefault()
                ?.RunProperties?.CloneNode(true);
            paragraph.RemoveAllChildren<Run>();
            var run = new Run();
            if (runProperties is not null)
            {
                run.Append(runProperties);
            }

            AppendText(run, replacedText);
            paragraph.Append(run);
        }
    }

    private static void AppendText(Run run, string value)
    {
        var lines = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                run.Append(new Break());
            }

            run.Append(new Text(lines[index]) { Space = SpaceProcessingModeValues.Preserve });
        }
    }

    private static void EnsureNoTokensRemain(OpenXmlElement root)
    {
        var unresolvedTokens = root.Descendants<Paragraph>()
            .SelectMany(paragraph => TokenPattern().Matches(GetText(paragraph)).Cast<Match>())
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unresolvedTokens.Length > 0)
        {
            throw new InvalidDataException(
                $"The generated document contains unresolved tokens: {string.Join(", ", unresolvedTokens)}.");
        }
    }

    private static string GetText(Paragraph paragraph) =>
        string.Concat(paragraph.Descendants<Text>().Select(text => text.Text));

    [GeneratedRegex(@"\{\{([A-Za-z][A-Za-z0-9]*)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}