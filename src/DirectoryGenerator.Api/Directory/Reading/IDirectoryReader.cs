using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory.Profiles;

namespace DirectoryGenerator.Api.Directory.Reading;

public interface IDirectoryReader
{
    Task<IReadOnlyList<DirectoryEntryPreview>> ReadAsync(
        DirectoryProfile profile,
        CancellationToken cancellationToken);
}