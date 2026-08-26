using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Controllers;
using DirectoryGenerator.Api.Directory;
using DirectoryGenerator.Api.Directory.Profiles;
using Microsoft.AspNetCore.Http;
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
        Descriptions = new Dictionary<string, string>
        {
            ["en-CA"] = "Employee phone directory"
        },
        Filter = "accountEnabled eq true",
        Properties = ["displayName"],
        Templates = new Dictionary<string, string>
        {
            ["en-CA"] = "Templates/default.en-CA.docx"
        }
    };

    [Fact]
    public async Task ReturnsRenderedWordDocumentForTrustedProfile()
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
        var renderer = new StubDirectoryDocumentRenderer();
        var generatedAt = new DateTimeOffset(2026, 8, 26, 14, 30, 0, TimeSpan.Zero);
        var controller = CreateController(reader, organizer, renderer, generatedAt);
        using var cancellationSource = new CancellationTokenSource();

        var result = await controller.GenerateDirectory(
            new GenerateDirectoryRequest("default", "en-CA"),
            cancellationSource.Token);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Same(renderer.Output, fileResult.FileStream);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileResult.ContentType);
        Assert.Equal(
            "directory-default-en-CA-20260826-143000.docx",
            fileResult.FileDownloadName);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        Assert.Same(Profile, reader.ReceivedProfile);
        Assert.Equal(cancellationSource.Token, reader.ReceivedCancellationToken);
        Assert.Same(expectedEntries, organizer.ReceivedEntries);
        Assert.Null(organizer.ReceivedSort);
        Assert.Equal("en-CA", organizer.ReceivedLocale);
        Assert.NotNull(renderer.ReceivedContent);
        Assert.Equal("default", renderer.ReceivedContent.ProfileId);
        Assert.Equal("en-CA", renderer.ReceivedContent.Locale);
        Assert.Equal("Default directory", renderer.ReceivedContent.Title);
        Assert.Equal("Employee phone directory", renderer.ReceivedContent.Description);
        Assert.Equal(generatedAt, renderer.ReceivedContent.GeneratedAt);
        Assert.Same(expectedGroups, renderer.ReceivedContent.Groups);
        Assert.Equal(cancellationSource.Token, renderer.ReceivedCancellationToken);
    }

    [Fact]
    public async Task ReturnsNotFoundForUnknownProfile()
    {
        var reader = new StubDirectoryReader([]);
        var organizer = new StubDirectoryEntryOrganizer([]);
        var renderer = new StubDirectoryDocumentRenderer();
        var controller = CreateController(reader, organizer, renderer);

        var result = await controller.GenerateDirectory(
            new GenerateDirectoryRequest("missing", "en-CA"),
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, problemResult.StatusCode);
        Assert.Null(reader.ReceivedProfile);
        Assert.Null(organizer.ReceivedEntries);
        Assert.Null(renderer.ReceivedContent);
    }

    [Fact]
    public async Task ReturnsBadRequestForUnsupportedLocale()
    {
        var reader = new StubDirectoryReader([]);
        var organizer = new StubDirectoryEntryOrganizer([]);
        var renderer = new StubDirectoryDocumentRenderer();
        var controller = CreateController(reader, organizer, renderer);

        var result = await controller.GenerateDirectory(
            new GenerateDirectoryRequest("default", "fr-CA"),
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problemResult.StatusCode);
        Assert.Null(reader.ReceivedProfile);
        Assert.Null(organizer.ReceivedEntries);
        Assert.Null(renderer.ReceivedContent);
    }

    private static DirectoriesController CreateController(
        StubDirectoryReader reader,
        StubDirectoryEntryOrganizer organizer,
        StubDirectoryDocumentRenderer renderer,
        DateTimeOffset? generatedAt = null)
    {
        var controller = new DirectoriesController(
            new StubProfileCatalog(Profile),
            reader,
            organizer,
            renderer,
            new StubTimeProvider(generatedAt ?? DateTimeOffset.UnixEpoch))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
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

    private sealed class StubDirectoryDocumentRenderer : IDirectoryDocumentRenderer
    {
        public MemoryStream Output { get; } = new([1, 2, 3]);

        public DirectoryDocumentContent? ReceivedContent { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Stream Render(
            DirectoryDocumentContent content,
            CancellationToken cancellationToken)
        {
            ReceivedContent = content;
            ReceivedCancellationToken = cancellationToken;
            return Output;
        }
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
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