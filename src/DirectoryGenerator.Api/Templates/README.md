# Word Templates

Templates are macro-free `.docx` files deployed with the API. Each profile maps every supported locale to a template path.

The starter templates contain:

- `{{documentTitle}}`, `{{documentDescription}}`, `{{generatedAt}}`, and `{{entryCount}}` document tokens.
- A `dg:groups` content control for the repeatable group section.
- A `dg:entries` content control around the prototype entry row.
- `{{group}}`, `{{displayName}}`, `{{jobTitle}}`, and `{{businessPhones}}` content tokens.

Edit the `.docx` files in Word to control layout and formatting. Keep the content-control tags and tokens intact. Run `scripts/New-StarterWordTemplates.ps1` only when you intentionally want to replace both templates with the minimal starter versions.