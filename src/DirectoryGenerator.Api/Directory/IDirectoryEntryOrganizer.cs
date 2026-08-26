using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory.Profiles;

namespace DirectoryGenerator.Api.Directory;

public interface IDirectoryEntryOrganizer
{
    IReadOnlyList<DirectoryGroupPreview> Organize(
        IReadOnlyList<DirectoryEntryPreview> entries,
        DirectorySort? sort,
        string locale);
}