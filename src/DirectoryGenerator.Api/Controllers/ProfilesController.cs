using DirectoryGenerator.Api.Auth;
using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryGenerator.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.GenerateDirectory)]
[Route("api/v1/profiles")]
public sealed class ProfilesController(IDirectoryProfileCatalog profileCatalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ProfileSummaryResponse>> GetProfiles()
    {
        var profiles = profileCatalog.Profiles
            .Select(profile => new ProfileSummaryResponse(
                profile.Id,
                GetDisplayName(profile),
                profile.DisplayNames.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();

        return Ok(profiles);
    }

    private static string GetDisplayName(DirectoryProfile profile) =>
        profile.DisplayNames.GetValueOrDefault("en-CA") ??
        profile.DisplayNames.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).First().Value;
}