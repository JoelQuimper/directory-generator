namespace DirectoryGenerator.Api.Directory.Templates;

public interface IDirectoryTemplateLoader
{
    Stream Open(string profileId, string locale);
}