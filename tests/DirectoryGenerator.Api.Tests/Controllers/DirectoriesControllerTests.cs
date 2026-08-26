using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Controllers;
using DirectoryGenerator.Api.Directory;
using DirectoryGenerator.Api.Directory.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryGenerator.Api.Tests.Controllers;

public sealed class DirectoriesControllerTests
{
    private static readonly DirectoryProfile Profile = new()
    {
        Id = "default",
        DisplayNames = new Dictionary<string, string>
        {
            ["en-CA"] = "Default directory"
        },
        Filter = "accountEnabled eq true",
        Properties = ["displayName"]
    };

    [Fact]
    public async Task ReturnsDirectoryPreviewForTrustedProfile()
    {
        var expectedEntries = new[]
        {
            new DirectoryEntryPreview(
                "user-id",
                "Ada Lovelace",
                "Ada",
                "Lovelace",
                null,
                null,
                null,
                null,
                [],
                null,
                null,
                "ada@example.com",
                true,
                "Member",
                null)
        };
        var reader = new StubDirectoryReader(expectedEntries);
        var expectedGroups = new[] { new DirectoryGroupPreview(null, expectedEntries) };
        var organizer = new StubDirectoryEntryOrganizer(expectedGroups);
        var controller = new DirectoriesController(
            new StubProfileCatalog(Profile),
            reader,
            organizer);
        using var cancellationSource = new CancellationTokenSource();

        var result = await controller.GenerateDirectory(
            new GenerateDirectoryRequest("default", "en-CA"),
            cancellationSource.Token);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expectedGroups, okResult.Value);
        Assert.Same(Profile, reader.ReceivedProfile);
        Assert.Equal(cancellationSource.Token, reader.ReceivedCancellationToken);
        Assert.Same(expectedEntries, organizer.ReceivedEntries);
        Assert.Null(organizer.ReceivedSort);
        Assert.Equal("en-CA", organizer.ReceivedLocale);
    }

    [Fact]
    public async Task ReturnsNotFoundForUnknownProfile()
    {
        var reader = new StubDirectoryReader([]);
        var organizer = new StubDirectoryEntryOrganizer([]);
        var controller = new DirectoriesController(
            new StubProfileCatalog(Profile),
            reader,
            organizer);

        var result = await controller.GenerateDirectory(
            new GenerateDirectoryRequest("missing", "en-CA"),
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, problemResult.StatusCode);
        Assert.Null(reader.ReceivedProfile);
        Assert.Null(organizer.ReceivedEntries);
    }

    [Fact]
    public async Task ReturnsBadRequestForUnsupportedLocale()
    {
        var reader = new StubDirectoryReader([]);
        var organizer = new StubDirectoryEntryOrganizer([]);
        var controller = new DirectoriesController(
            new StubProfileCatalog(Profile),
            reader,
            organizer);

        var result = await controller.GenerateDirectory(
            new GenerateDirectoryRequest("default", "fr-CA"),
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, problemResult.StatusCode);
        Assert.Null(reader.ReceivedProfile);
        Assert.Null(organizer.ReceivedEntries);
    }

    private sealed class StubDirectoryEntryOrganizer(
        IReadOnlyList<DirectoryGroupPreview> groups) : IDirectoryEntryOrganizer
    {
        public IReadOnlyList<DirectoryEntryPreview>? ReceivedEntries { get; private set; }

        public DirectorySort? ReceivedSort { get; private set; }

        public string? ReceivedLocale { get; private set; }

        public IReadOnlyList<DirectoryGroupPreview> Organize(
            IReadOnlyList<DirectoryEntryPreview> entries,
            DirectorySort? sort,
            string locale)
        {
            ReceivedEntries = entries;
            ReceivedSort = sort;
            ReceivedLocale = locale;
            return groups;
        }
    }

    private sealed class StubDirectoryReader(IReadOnlyList<DirectoryEntryPreview> entries)
        : IDirectoryReader
    {
        public DirectoryProfile? ReceivedProfile { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IReadOnlyList<DirectoryEntryPreview>> ReadAsync(
            DirectoryProfile profile,
            CancellationToken cancellationToken)
        {
            ReceivedProfile = profile;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(entries);
        }
    }

    private sealed class StubProfileCatalog(params DirectoryProfile[] profiles)
        : IDirectoryProfileCatalog
    {
        public IReadOnlyList<DirectoryProfile> Profiles { get; } = profiles;

        public DirectoryProfile? Find(string id) =>
            Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}