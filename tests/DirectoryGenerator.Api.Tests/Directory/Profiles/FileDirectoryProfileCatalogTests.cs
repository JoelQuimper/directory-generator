using DirectoryGenerator.Api.Directory.Profiles;

namespace DirectoryGenerator.Api.Tests.Directory.Profiles;

public sealed class FileDirectoryProfileCatalogTests
{
    [Fact]
    public void LoadsValidProfiles()
    {
        using var directory = new TemporaryProfileDirectory();
        directory.Write("default.json", ValidProfileJson);

        var catalog = new FileDirectoryProfileCatalog(directory.Path);

        var profile = Assert.Single(catalog.Profiles);
        Assert.Equal("default", profile.Id);
        Assert.Same(profile, catalog.Find("DEFAULT"));
    }

    [Fact]
    public void RejectsQueryParametersInFilter()
    {
        using var directory = new TemporaryProfileDirectory();
        directory.Write(
            "unsafe.json",
            ValidProfileJson.Replace(
                "accountEnabled eq true",
                "accountEnabled eq true&$select=displayName",
                StringComparison.Ordinal));

        var exception = Assert.Throws<InvalidDataException>(
            () => new FileDirectoryProfileCatalog(directory.Path));

        Assert.Contains("without URLs or query parameters", exception.Message);
    }

    [Fact]
    public void RejectsDuplicateProfileIds()
    {
        using var directory = new TemporaryProfileDirectory();
        directory.Write("first.json", ValidProfileJson);
        directory.Write("second.json", ValidProfileJson);

        var exception = Assert.Throws<InvalidDataException>(
            () => new FileDirectoryProfileCatalog(directory.Path));

        Assert.Contains("ID 'default' is duplicated", exception.Message);
    }

    private const string ValidProfileJson = """
        {
          "id": "default",
          "displayNames": {
            "en-CA": "Default directory",
            "fr-CA": "Repertoire par defaut"
          },
          "filter": "accountEnabled eq true",
          "properties": [
            "id",
            "displayName",
            "department"
          ],
          "sort": {
            "groupBy": {
              "property": "department",
              "direction": "ascending"
            },
            "entries": [
              {
                "property": "displayName",
                "direction": "ascending"
              }
            ]
          }
        }
        """;

    private sealed class TemporaryProfileDirectory : IDisposable
    {
        public TemporaryProfileDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"directory-generator-tests-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Write(string fileName, string contents) =>
            File.WriteAllText(System.IO.Path.Combine(Path, fileName), contents);

        public void Dispose() =>
            System.IO.Directory.Delete(Path, recursive: true);
    }
}