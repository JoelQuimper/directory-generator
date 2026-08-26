using DirectoryGenerator.Api.Directory;
using DirectoryGenerator.Api.Directory.Profiles;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DirectoryGenerator.Api.Tests.Directory;

public sealed class FileDirectoryTemplateLoaderTests
{
    [Theory]
    [InlineData("default.en-CA.docx")]
    [InlineData("default.fr-CA.docx")]
    public void DeployedStarterTemplateIsSchemaValid(string fileName)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Templates", fileName);
        using var document = WordprocessingDocument.Open(path, false);

        var errors = new OpenXmlValidator().Validate(document).ToArray();

        Assert.True(
            errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(error =>
                $"{error.Path?.XPath}: {error.Description}")));

        var body = Assert.IsType<Body>(document.MainDocumentPart!.Document.Body);
        Assert.IsType<Paragraph>(body.Elements().ElementAt(body.Elements().Count() - 2));

        var outerTable = body.Descendants<Table>().First();
        var leftCell = outerTable.Elements<TableRow>().Single().Elements<TableCell>().First();
        Assert.IsType<Paragraph>(leftCell.LastChild);
    }

    [Fact]
    public void OpensIndependentStreamsForValidTemplate()
    {
        using var directory = new TemporaryTemplateDirectory();
        directory.WriteTemplate("Templates/default.en-CA.docx");
        var loader = new FileDirectoryTemplateLoader(
            directory.Path,
            new StubProfileCatalog(CreateProfile("Templates/default.en-CA.docx")));

        using var first = loader.Open("default", "en-CA");
        using var second = loader.Open("DEFAULT", "EN-ca");

        Assert.NotSame(first, second);
        Assert.True(first.Length > 0);
        Assert.Equal(first.Length, second.Length);
        _ = first.ReadByte();
        Assert.Equal(1, first.Position);
        Assert.Equal(0, second.Position);
    }

    [Fact]
    public void RejectsMissingTemplate()
    {
        using var directory = new TemporaryTemplateDirectory();

        var exception = Assert.Throws<InvalidDataException>(() =>
            new FileDirectoryTemplateLoader(
                directory.Path,
                new StubProfileCatalog(CreateProfile("Templates/missing.docx"))));

        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public void RejectsInvalidDocxPackage()
    {
        using var directory = new TemporaryTemplateDirectory();
        directory.WriteText("Templates/invalid.docx", "not a DOCX package");

        var exception = Assert.Throws<InvalidDataException>(() =>
            new FileDirectoryTemplateLoader(
                directory.Path,
                new StubProfileCatalog(CreateProfile("Templates/invalid.docx"))));

        Assert.Contains("not a valid DOCX package", exception.Message);
    }

    [Fact]
    public void RejectsMacroEnabledDocument()
    {
        using var directory = new TemporaryTemplateDirectory();
        directory.WriteTemplate(
            "Templates/macro.docx",
            WordprocessingDocumentType.MacroEnabledDocument);

        var exception = Assert.Throws<InvalidDataException>(() =>
            new FileDirectoryTemplateLoader(
                directory.Path,
                new StubProfileCatalog(CreateProfile("Templates/macro.docx"))));

        Assert.Contains("macro-free DOCX", exception.Message);
    }

    private static DirectoryProfile CreateProfile(string templatePath) =>
        new()
        {
            Id = "default",
            DisplayNames = new Dictionary<string, string>
            {
                ["en-CA"] = "Default directory"
            },
            Filter = "accountEnabled eq true",
            Properties = ["displayName"],
            Templates = new Dictionary<string, string>
            {
                ["en-CA"] = templatePath
            }
        };

    private sealed class StubProfileCatalog(params DirectoryProfile[] profiles)
        : IDirectoryProfileCatalog
    {
        public IReadOnlyList<DirectoryProfile> Profiles { get; } = profiles;

        public DirectoryProfile? Find(string id) =>
            Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TemporaryTemplateDirectory : IDisposable
    {
        public TemporaryTemplateDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"directory-generator-template-tests-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void WriteTemplate(
            string relativePath,
            WordprocessingDocumentType documentType = WordprocessingDocumentType.Document)
        {
            var path = PreparePath(relativePath);
            using var document = WordprocessingDocument.Create(path, documentType);
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("Template")))));
            mainPart.Document.Save();
        }

        public void WriteText(string relativePath, string contents) =>
            File.WriteAllText(PreparePath(relativePath), contents);

        public void Dispose() =>
            System.IO.Directory.Delete(Path, recursive: true);

        private string PreparePath(string relativePath)
        {
            var path = System.IO.Path.Combine(Path, relativePath);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            return path;
        }
    }
}