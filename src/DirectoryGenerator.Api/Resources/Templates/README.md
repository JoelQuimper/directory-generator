# Word Templates

Templates are macro-free `.docx` files deployed with the API. Each profile maps every supported locale to a template path.

The starter templates contain:

- `{{documentTitle}}` and `{{documentDescription}}` centered above the directory.
- `{{generatedAt}}` and `{{entryCount}}` in the page footer.
- A landscape, two-cell outer table for deterministic column balancing.
- A single `dg:groups` content control around the complete two-column directory region.
- A `dg:entries` content control around the prototype entry row.
- `{{group}}` in the prototype group heading.
- `{{businessPhones}}`, `{{displayName}}`, and `{{jobTitle}}` columns in each prototype entry row.

The renderer owns both outer cells. It keeps groups intact when possible, fills the left cell to its target capacity, and then places remaining groups in the right cell. The left cell contains the prototype formatting; the initially empty right cell is the second rendering target.

Edit the `.docx` files in Word to control layout and formatting. Keep the content-control tags and tokens intact. Run `scripts/New-StarterWordTemplates.ps1` only when you intentionally want to replace both templates with the minimal starter versions.