using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory.Rendering;
using DirectoryGenerator.Api.Directory.Templates;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DirectoryGenerator.Api.Tests.Directory;

public sealed class OpenXmlDirectoryDocumentRendererTests
{
    [Fact]
    public void RendersBalancedDirectoryFromDeployedTemplate()
    {
        var renderer = new OpenXmlDirectoryDocumentRenderer(
            new StubTemplateLoader(ReadTemplate()));
        var groups = new[]
        {
            new DirectoryGroupPreview("Accounting",
            [
                CreateEntry("Ada Lovelace", "Senior accountant", "222", "223"),
                CreateEntry("Grace Hopper", "Accounting clerk", "224")
            ]),
            new DirectoryGroupPreview("Facilities",
            [
                CreateEntry("Katherine Johnson", "Facilities manager", "272")
            ])
        };

        using var output = renderer.Render(
            new DirectoryDocumentContent(
                "default",
                "en-CA",
                "District phone directory",
                "Internal extensions",
                new DateTimeOffset(2026, 8, 26, 14, 30, 0, TimeSpan.Zero),
                groups),
            CancellationToken.None);
        using var document = WordprocessingDocument.Open(output, false);

        var validationErrors = new OpenXmlValidator().Validate(document).ToArray();
        Assert.True(
            validationErrors.Length == 0,
            string.Join(Environment.NewLine, validationErrors.Select(error => error.Description)));

        var body = Assert.IsType<Body>(document.MainDocumentPart!.Document.Body);
        var bodyText = body.InnerText;
        Assert.Contains("District phone directory", bodyText);
        Assert.Contains("Internal extensions", bodyText);
        Assert.Contains("Accounting", bodyText);
        Assert.Contains("Ada Lovelace", bodyText);
        Assert.Contains("Facilities", bodyText);
        Assert.Contains("Katherine Johnson", bodyText);
        Assert.DoesNotContain("{{", bodyText);
        Assert.Single(body.Descendants<Break>());

        var outerTable = Assert.Single(body.Elements<Table>());
        var outerRow = Assert.Single(outerTable.Elements<TableRow>());
        var columns = outerRow.Elements<TableCell>().ToArray();
        Assert.Equal(2, columns.Length);
        Assert.Single(columns[0].Elements<Table>());
        Assert.Single(columns[1].Elements<Table>());

        var footer = Assert.Single(document.MainDocumentPart.FooterParts).Footer;
        Assert.Contains("3 entries", footer.InnerText);
        Assert.DoesNotContain("{{", footer.InnerText);
    }

    [Fact]
    public void ReplacesTokenSplitAcrossRuns()
    {
        var template = RewriteTemplate(document =>
        {
            var title = document.MainDocumentPart!.Document.Body!.Elements<Paragraph>().First();
            title.RemoveAllChildren<Run>();
            title.Append(
                new Run(new Text("{{document")),
                new Run(new Text("Title}}")));
        });
        var renderer = new OpenXmlDirectoryDocumentRenderer(new StubTemplateLoader(template));

        using var output = renderer.Render(
            new DirectoryDocumentContent(
                "default",
                "en-CA",
                "Split token title",
                null,
                DateTimeOffset.UnixEpoch,
                []),
            CancellationToken.None);
        using var document = WordprocessingDocument.Open(output, false);

        Assert.Contains("Split token title", document.MainDocumentPart!.Document.InnerText);
        Assert.DoesNotContain("{{documentTitle}}", document.MainDocumentPart.Document.InnerText);
    }

    [Fact]
    public void RejectsUnsupportedToken()
    {
        var template = RewriteTemplate(document =>
        {
            document.MainDocumentPart!.Document.Body!.PrependChild(
                new Paragraph(new Run(new Text("{{unsupportedValue}}"))));
        });
        var renderer = new OpenXmlDirectoryDocumentRenderer(new StubTemplateLoader(template));

        var exception = Assert.Throws<InvalidDataException>(() => renderer.Render(
            new DirectoryDocumentContent(
                "default",
                "en-CA",
                "Directory",
                null,
                DateTimeOffset.UnixEpoch,
                []),
            CancellationToken.None));

        Assert.Contains("unsupportedValue", exception.Message);
    }

    private static DirectoryEntryPreview CreateEntry(
        string displayName,
        string jobTitle,
        params string[] phones) =>
        new(
            Guid.NewGuid().ToString(),
            displayName,
            null,
            null,
            jobTitle,
            null,
            null,
            null,
            phones,
            null,
            null,
            null,
            true,
            "Member",
            null);

    private static byte[] RewriteTemplate(Action<WordprocessingDocument> rewrite)
    {
        using var stream = new MemoryStream(ReadTemplate());
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            rewrite(document);
            document.MainDocumentPart!.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] ReadTemplate() =>
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "Templates",
            "default.en-CA.docx"));

    private sealed class StubTemplateLoader(byte[] template) : IDirectoryTemplateLoader
    {
        public Stream Open(string profileId, string locale) =>
            new MemoryStream(template, writable: false);
    }
}