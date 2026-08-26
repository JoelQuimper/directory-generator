namespace DirectoryGenerator.Api.Directory.Profiles;

public interface IDirectoryProfileCatalog
{
    IReadOnlyList<DirectoryProfile> Profiles { get; }

    DirectoryProfile? Find(string id);
}