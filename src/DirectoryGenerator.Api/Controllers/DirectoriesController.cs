using DirectoryGenerator.Api.Auth;
using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory;
using DirectoryGenerator.Api.Directory.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryGenerator.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.GenerateDirectory)]
[Route("api/v1/directories")]
public sealed class DirectoriesController(
    IDirectoryProfileCatalog profileCatalog,
    IDirectoryReader directoryReader,
    IDirectoryEntryOrganizer entryOrganizer) : ControllerBase
{
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DirectoryGroupPreview>>> GenerateDirectory(
        [FromBody] GenerateDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var profile = profileCatalog.Find(request.ProfileId);
        if (profile is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Directory profile not found.",
                detail: $"Directory profile '{request.ProfileId}' does not exist.");
        }

        if (!profile.DisplayNames.ContainsKey(request.Locale))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Directory locale is not supported.",
                detail: $"Directory profile '{profile.Id}' does not support locale '{request.Locale}'.");
        }

        var entries = await directoryReader.ReadAsync(profile, cancellationToken);
        var organizedEntries = entryOrganizer.Organize(entries, profile.Sort, request.Locale);
        return Ok(organizedEntries);
    }
}