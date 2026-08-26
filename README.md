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
- `tools/DirectoryGenerator.Console/appsettings.Development.json`

## License

This project is licensed under the [MIT License](LICENSE).
