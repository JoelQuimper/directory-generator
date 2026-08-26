using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Controllers;
using DirectoryGenerator.Api.Directory.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryGenerator.Api.Tests.Controllers;

public sealed class ProfilesControllerTests
{
    [Fact]
    public void ReturnsSafeProfileMetadata()
    {
        var profile = new DirectoryProfile
        {
            Id = "default",
            DisplayNames = new Dictionary<string, string>
            {
                ["en-CA"] = "Default directory",
                ["fr-CA"] = "Repertoire par defaut"
            },
            Filter = "accountEnabled eq true",
            Properties = ["displayName"]
        };
        var controller = new ProfilesController(new StubProfileCatalog(profile));

        var result = controller.GetProfiles();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var summaries = Assert.IsType<ProfileSummaryResponse[]>(okResult.Value);
        var summary = Assert.Single(summaries);
        Assert.Equal("default", summary.Id);
        Assert.Equal("Default directory", summary.DisplayName);
        Assert.Equal(["en-CA", "fr-CA"], summary.SupportedLocales);
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