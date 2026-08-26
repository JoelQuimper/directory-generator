# Directory Generator Console

This development tool verifies application access to the Directory Generator API using the OAuth 2.0 client credentials flow.

## Prerequisites

- Run `scripts/New-EntraAppRegistrations.ps1` and use its Console JSON output as `appsettings.Development.json` in this directory.
- Sign in to Azure CLI as the user who owns the Directory Generator Console app registration.
- Start the API at the configured `ApiBaseUrl`.

## Run

```powershell
dotnet run --project tools/DirectoryGenerator.Console
```

The tool uses `AzureCliCredential` to authenticate to Microsoft Graph, creates a uniquely named one-hour credential for the Console registration, requests `api://{ApiClientId}/.default`, and calls `POST /api/v1/directories/generate` with the default profile. A successful response is saved to the current directory using the server-provided `.docx` filename. The tool deletes the exact temporary credential through Microsoft Graph in a `finally` block and never prints the credential or access token.

The one-hour expiry limits exposure if the process is forcibly terminated before cleanup can run.