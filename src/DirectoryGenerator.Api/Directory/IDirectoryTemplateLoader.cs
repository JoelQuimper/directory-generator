namespace DirectoryGenerator.Api.Directory;

public interface IDirectoryTemplateLoader
{
    Stream Open(string profileId, string locale);
}