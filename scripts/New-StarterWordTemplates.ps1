[CmdletBinding()]
param(
  [string] $OutputDirectory = (Join-Path $PSScriptRoot "..\src\DirectoryGenerator.Api\Resources\Templates")
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
  <Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
</Types>
'@

    $relationships = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
'@

    $documentRelationships = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rIdFooter1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
</Relationships>
'@

    $footer = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p>
    <w:pPr><w:spacing w:before="0" w:after="0"/><w:jc w:val="right"/></w:pPr>
    <w:r><w:rPr><w:sz w:val="16"/><w:lang w:val="$Language"/></w:rPr><w:t>Generated {{generatedAt}} | {{entryCount}} entries</w:t></w:r>
  </w:p>
</w:ftr>
"@

    $document = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    <w:p>
      <w:pPr><w:spacing w:before="0" w:after="40"/><w:jc w:val="center"/></w:pPr>
      <w:r><w:rPr><w:b/><w:sz w:val="24"/><w:lang w:val="$Language"/></w:rPr><w:t>{{documentTitle}}</w:t></w:r>
    </w:p>
    <w:p>
      <w:pPr><w:spacing w:before="0" w:after="100"/><w:jc w:val="center"/></w:pPr>
      <w:r><w:rPr><w:i/><w:sz w:val="18"/><w:lang w:val="$Language"/></w:rPr><w:t>{{documentDescription}}</w:t></w:r>
    </w:p>
    <w:sdt>
      <w:sdtPr><w:tag w:val="dg:groups"/></w:sdtPr>
      <w:sdtContent>
        <w:tbl>
          <w:tblPr>
            <w:tblW w:w="0" w:type="auto"/>
            <w:tblBorders>
              <w:top w:val="nil"/><w:left w:val="nil"/><w:bottom w:val="nil"/><w:right w:val="nil"/>
              <w:insideH w:val="nil"/><w:insideV w:val="nil"/>
            </w:tblBorders>
            <w:tblCellMar><w:left w:w="0" w:type="dxa"/><w:right w:w="0" w:type="dxa"/></w:tblCellMar>
          </w:tblPr>
          <w:tblGrid><w:gridCol w:w="7160"/><w:gridCol w:w="7160"/></w:tblGrid>
          <w:tr>
            <w:tc>
              <w:tcPr><w:tcW w:w="7160" w:type="dxa"/><w:tcMar><w:right w:w="100" w:type="dxa"/></w:tcMar></w:tcPr>
              <w:tbl>
                <w:tblPr>
                  <w:tblW w:w="0" w:type="auto"/>
                  <w:tblBorders>
                    <w:top w:val="single" w:sz="4"/><w:left w:val="single" w:sz="4"/><w:bottom w:val="single" w:sz="4"/><w:right w:val="single" w:sz="4"/>
                    <w:insideH w:val="single" w:sz="4"/><w:insideV w:val="single" w:sz="4"/>
                  </w:tblBorders>
                </w:tblPr>
                <w:tblGrid><w:gridCol w:w="1300"/><w:gridCol w:w="2200"/><w:gridCol w:w="3660"/></w:tblGrid>
                <w:tr>
                  <w:trPr><w:cantSplit/></w:trPr>
                  <w:tc>
                    <w:tcPr><w:gridSpan w:val="3"/><w:shd w:val="clear" w:fill="D9D9D9"/></w:tcPr>
                    <w:p><w:pPr><w:spacing w:before="0" w:after="0"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val="16"/></w:rPr><w:t>{{group}}</w:t></w:r></w:p>
                  </w:tc>
                </w:tr>
                <w:sdt>
                  <w:sdtPr><w:tag w:val="dg:entries"/></w:sdtPr>
                  <w:sdtContent>
                    <w:tr>
                      <w:trPr><w:cantSplit/></w:trPr>
                      <w:tc><w:tcPr><w:tcW w:w="1300" w:type="dxa"/></w:tcPr><w:p><w:pPr><w:spacing w:before="0" w:after="0"/><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:sz w:val="16"/></w:rPr><w:t>{{businessPhones}}</w:t></w:r></w:p></w:tc>
                      <w:tc><w:tcPr><w:tcW w:w="2200" w:type="dxa"/></w:tcPr><w:p><w:pPr><w:spacing w:before="0" w:after="0"/></w:pPr><w:r><w:rPr><w:sz w:val="16"/></w:rPr><w:t>{{displayName}}</w:t></w:r></w:p></w:tc>
                      <w:tc><w:tcPr><w:tcW w:w="3660" w:type="dxa"/></w:tcPr><w:p><w:pPr><w:spacing w:before="0" w:after="0"/></w:pPr><w:r><w:rPr><w:sz w:val="16"/></w:rPr><w:t>{{jobTitle}}</w:t></w:r></w:p></w:tc>
                    </w:tr>
                  </w:sdtContent>
                </w:sdt>
              </w:tbl>
              <w:p/>
            </w:tc>
            <w:tc>
              <w:tcPr><w:tcW w:w="7160" w:type="dxa"/><w:tcMar><w:left w:w="100" w:type="dxa"/></w:tcMar></w:tcPr>
              <w:p><w:pPr><w:spacing w:before="0" w:after="0"/></w:pPr></w:p>
            </w:tc>
          </w:tr>
        </w:tbl>
      </w:sdtContent>
    </w:sdt>
    <w:p/>
    <w:sectPr>
      <w:footerReference w:type="default" r:id="rIdFooter1"/>
      <w:pgSz w:w="15840" w:h="12240" w:orient="landscape"/>
      <w:pgMar w:top="540" w:right="720" w:bottom="720" w:left="720" w:header="360" w:footer="360" w:gutter="0"/>
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
            Add-ZipTextEntry $archive "word/_rels/document.xml.rels" $documentRelationships
            Add-ZipTextEntry $archive "word/footer1.xml" $footer
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