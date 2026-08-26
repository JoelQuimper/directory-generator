using DirectoryGenerator.Api.Contracts;

namespace DirectoryGenerator.Api.Directory;

public sealed record DirectoryDocumentContent(
    string ProfileId,
    string Locale,
    string Title,
    string? Description,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DirectoryGroupPreview> Groups);

public interface IDirectoryDocumentRenderer
{
    Stream Render(
        DirectoryDocumentContent content,
        CancellationToken cancellationToken);
}