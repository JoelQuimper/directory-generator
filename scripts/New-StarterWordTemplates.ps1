[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot "..\src\DirectoryGenerator.Api\Templates")
)

$ErrorActionPreference = "Stop"

function Add-ZipTextEntry {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchive] $Archive,

        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Content
    )

    $entry = $Archive.CreateEntry($Path, [System.IO.Compression.CompressionLevel]::Optimal)
    $stream = $entry.Open()
    $writer = [System.IO.StreamWriter]::new(
        $stream,
        [System.Text.UTF8Encoding]::new($false))

    try {
        $writer.Write($Content)
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function New-StarterWordTemplate {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Language
    )

    $contentTypes = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
</Types>
'@

    $relationships = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
'@

    $document = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    <w:p>
      <w:pPr><w:pStyle w:val="Title"/></w:pPr>
      <w:r><w:rPr><w:lang w:val="$Language"/></w:rPr><w:t>{{documentTitle}}</w:t></w:r>
    </w:p>
    <w:p><w:r><w:t>{{documentDescription}}</w:t></w:r></w:p>
    <w:p><w:r><w:t>Generated {{generatedAt}} | {{entryCount}} entries</w:t></w:r></w:p>
    <w:sdt>
      <w:sdtPr><w:tag w:val="dg:groups"/></w:sdtPr>
      <w:sdtContent>
        <w:p><w:r><w:t>{{group}}</w:t></w:r></w:p>
        <w:tbl>
          <w:tblPr><w:tblW w:w="0" w:type="auto"/></w:tblPr>
          <w:tblGrid><w:gridCol/><w:gridCol/><w:gridCol/></w:tblGrid>
          <w:sdt>
            <w:sdtPr><w:tag w:val="dg:entries"/></w:sdtPr>
            <w:sdtContent>
              <w:tr>
                <w:tc><w:p><w:r><w:t>{{displayName}}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>{{jobTitle}}</w:t></w:r></w:p></w:tc>
                <w:tc><w:p><w:r><w:t>{{businessPhones}}</w:t></w:r></w:p></w:tc>
              </w:tr>
            </w:sdtContent>
          </w:sdt>
        </w:tbl>
      </w:sdtContent>
    </w:sdt>
    <w:sectPr>
      <w:pgSz w:w="12240" w:h="15840"/>
      <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>
    </w:sectPr>
  </w:body>
</w:document>
"@

    $fileStream = [System.IO.File]::Create($Path)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $fileStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            Add-ZipTextEntry $archive "[Content_Types].xml" $contentTypes
            Add-ZipTextEntry $archive "_rels/.rels" $relationships
            Add-ZipTextEntry $archive "word/document.xml" $document
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $fileStream.Dispose()
    }
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

New-StarterWordTemplate `
    -Path (Join-Path $resolvedOutputDirectory "default.en-CA.docx") `
    -Language "en-CA"
New-StarterWordTemplate `
    -Path (Join-Path $resolvedOutputDirectory "default.fr-CA.docx") `
    -Language "fr-CA"

Write-Verbose "Created starter Word templates in '$resolvedOutputDirectory'."