using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DirectoryGenerator.Api.Directory.Profiles;

namespace DirectoryGenerator.Api.Directory;

public sealed class FileDirectoryTemplateLoader : IDirectoryTemplateLoader
{
    private readonly IReadOnlyDictionary<string, byte[]> templates;

    public FileDirectoryTemplateLoader(
        string contentRootPath,
        IDirectoryProfileCatalog profileCatalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ArgumentNullException.ThrowIfNull(profileCatalog);

        var contentRoot = Path.GetFullPath(contentRootPath);
        templates = profileCatalog.Profiles
            .SelectMany(profile => profile.Templates.Select(template => new
            {
                Key = CreateKey(profile.Id, template.Key),
                ProfileId = profile.Id,
                Locale = template.Key,
                Path = ResolvePath(contentRoot, template.Value)
            }))
            .ToDictionary(
                template => template.Key,
                template => LoadAndValidate(template.Path, template.ProfileId, template.Locale),
                StringComparer.OrdinalIgnoreCase);
    }

    public Stream Open(string profileId, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        if (!templates.TryGetValue(CreateKey(profileId, locale), out var contents))
        {
            throw new KeyNotFoundException(
                $"Word template for profile '{profileId}' and locale '{locale}' was not found.");
        }

        return new MemoryStream(contents, writable: false);
    }

    private static string ResolvePath(string contentRoot, string relativePath)
    {
        var path = Path.GetFullPath(relativePath, contentRoot);
        var rootPrefix = Path.TrimEndingDirectorySeparator(contentRoot) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Word template path '{relativePath}' is outside the API content root.");
        }

        return path;
    }

    private static byte[] LoadAndValidate(string path, string profileId, string locale)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException(
                $"Word template for profile '{profileId}' and locale '{locale}' does not exist.");
        }

        var contents = File.ReadAllBytes(path);
        try
        {
            using var stream = new MemoryStream(contents, writable: false);
            using var document = WordprocessingDocument.Open(stream, false);
            if (document.DocumentType != WordprocessingDocumentType.Document ||
                document.MainDocumentPart?.Document is null)
            {
                throw new InvalidDataException(
                    $"Word template for profile '{profileId}' and locale '{locale}' must be a macro-free DOCX document.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or FileFormatException or OpenXmlPackageException)
        {
            throw new InvalidDataException(
                $"Word template for profile '{profileId}' and locale '{locale}' is not a valid DOCX package.",
                exception);
        }

        return contents;
    }

    private static string CreateKey(string profileId, string locale) =>
        $"{profileId}\u001f{locale}";
}