# Directory Generator

Directory Generator is a planned ASP.NET Core Web API that retrieves a profile-defined set of users from Microsoft Entra ID and generates a Microsoft Word staff phone book.

> [!WARNING]
> This is an early-stage community project provided as-is. There is no support commitment, service-level agreement, or guarantee of fitness for production use. Review the code, identity permissions, security configuration, privacy implications, and generated output for your environment before using it with organizational directory data. Use at your own risk.

The application is currently in the design phase and is not yet implemented. See the [architecture and development plan](docs/architecture-and-development-plan.md) for the proposed scope, security model, and implementation sequence.

## One-time Entra setup

The `scripts/New-EntraAppRegistrations.ps1` script is intended to be run once for an environment. It creates these three Microsoft Entra app registrations and their enterprise applications:

- Directory Generator API
- Directory Generator Swagger
- Directory Generator Console

The script is not idempotent. Running it again creates another set of registrations instead of updating the existing set.

To provision a replacement set, first delete the three existing app registrations and remove their enterprise applications if they remain. Run the script again, then replace the values in these ignored local configuration files with the newly generated output:

- `src/DirectoryGenerator.Api/appsettings.Development.json`
- `src/DirectoryGenerator.Console/appsettings.Development.json`

## Test directory users

The `scripts/New-EntraTestUsers.ps1` script creates 60 synthetic, enabled Microsoft Entra users distributed evenly across 10 departments. The department set includes `Core` and `Test`. Each user has a given name, surname, display name, department, job title, and business phone extension.

The script uses the current Azure CLI user and requires Microsoft Graph `User.ReadWrite.All` or `Directory.ReadWrite.All` plus an appropriate Microsoft Entra role. Pass a verified domain from the target tenant. Deterministic `dg-test-001` through `dg-test-060` user principal names make reruns safe: existing users are skipped, and generated passwords are never printed or persisted.

Preview locally without contacting Azure:

```powershell
.\scripts\New-EntraTestUsers.ps1 -TenantDomain "example.onmicrosoft.com" -WhatIf
```

After reviewing the preview, run the same command without `-WhatIf` yourself to create the users.

## Infrastructure deployment

Replace the Microsoft Entra application ID placeholders in `infra/main.bicepparam`, then run:

```powershell
.\scripts\Deploy-Infrastructure.ps1
```

The script requests confirmation before deploying and uses the active Azure CLI subscription by default. Pass `-SubscriptionId <subscription-id>` to target a subscription without changing the active Azure CLI context. After deployment, it idempotently grants the App Service managed identity the Microsoft Graph application permission `User.Read.All`. The signed-in Azure CLI user must have permission to create Microsoft Graph app-role assignments.

## License

This project is licensed under the [MIT License](LICENSE).
