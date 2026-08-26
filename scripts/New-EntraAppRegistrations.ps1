[CmdletBinding()]
param(
    [string] $ApiDisplayName = "Directory Generator API",

    [string] $SwaggerDisplayName = "Directory Generator Swagger",

    [uri] $SwaggerRedirectUri = "http://localhost:5193/swagger/oauth2-redirect.html",

    [string] $ConsoleDisplayName = "Directory Generator Console"
)

$ErrorActionPreference = "Stop"
$VerbosePreference = "Continue"
$PSNativeCommandUseErrorActionPreference = $true

# Use the current Azure CLI login and show its context before making changes.
Write-Verbose "Reading the current Azure CLI context."
$account = az account show `
    --only-show-errors `
    --output json | ConvertFrom-Json

if ($account.user.type -ne "user") {
    throw "This script requires an interactive Azure CLI user login so the user can own the app registrations."
}

$tenantId = $account.tenantId
$owner = az ad signed-in-user show `
    --only-show-errors `
    --output json | ConvertFrom-Json
$ownerId = $owner.id

Write-Verbose "Signed-in user: $($account.user.name)"
Write-Verbose "User type: $($account.user.type)"
Write-Verbose "Owner object ID: $ownerId"
Write-Verbose "Tenant ID: $tenantId"
Write-Verbose "Subscription: $($account.name) ($($account.id))"
Write-Verbose "Environment: $($account.environmentName)"

function Add-CurrentUserAsApplicationOwner {
    param(
        [Parameter(Mandatory)]
        [string] $ApplicationObjectId,

        [Parameter(Mandatory)]
        [string] $ApplicationDisplayName
    )

    $ownerIds = @(az ad app owner list `
        --id $ApplicationObjectId `
        --query "[].id" `
        --only-show-errors `
        --output tsv)

    if ($ownerId -in $ownerIds) {
        Write-Verbose "Signed-in user is already an owner of '$ApplicationDisplayName'."
        return
    }

    Write-Verbose "Adding the signed-in user as an owner of '$ApplicationDisplayName'."
    az ad app owner add `
        --id $ApplicationObjectId `
        --owner-object-id $ownerId `
        --only-show-errors `
        --output none
    Write-Verbose "Added the signed-in user as an owner of '$ApplicationDisplayName'."
}

# The delegated scope and app role require stable IDs within each new registration.
$scopeId = [guid]::NewGuid().ToString()
$roleId = [guid]::NewGuid().ToString()

# Create the protected web API registration.
Write-Verbose "Creating API app registration '$ApiDisplayName'."
$apiApplication = az ad app create `
    --display-name $ApiDisplayName `
    --sign-in-audience AzureADMyOrg `
    --only-show-errors `
    --output json | ConvertFrom-Json
Write-Verbose "Created API app registration with client ID '$($apiApplication.appId)'."
Add-CurrentUserAsApplicationOwner `
    -ApplicationObjectId $apiApplication.id `
    -ApplicationDisplayName $ApiDisplayName

# Expose the delegated scope used to acquire user tokens and the app role required by API authorization.
$apiBody = @{
    identifierUris = @("api://$($apiApplication.appId)")
    api = @{
        requestedAccessTokenVersion = 2
        oauth2PermissionScopes = @(
            @{
                id = $scopeId
                adminConsentDescription = "Allow the application to access Directory Generator on behalf of the signed-in user."
                adminConsentDisplayName = "Access Directory Generator"
                isEnabled = $true
                type = "User"
                userConsentDescription = "Allow this application to access Directory Generator on your behalf."
                userConsentDisplayName = "Access Directory Generator"
                value = "Directory.Access"
            }
        )
    }
    appRoles = @(
        @{
            id = $roleId
            allowedMemberTypes = @("User", "Application")
            description = "Generate directory documents."
            displayName = "Generate directories"
            isEnabled = $true
            value = "Directory.Generate"
        }
    )
} | ConvertTo-Json -Depth 10

# Passing the body from a file preserves JSON quotes across PowerShell's native command boundary.
$apiBodyPath = [System.IO.Path]::GetTempFileName()
$apiBody | Set-Content -LiteralPath $apiBodyPath -Encoding utf8NoBOM

Write-Verbose "Adding Directory.Access scope and Directory.Generate app role to the API registration."
try {
    az rest `
        --method PATCH `
        --uri "https://graph.microsoft.com/v1.0/applications/$($apiApplication.id)" `
        --headers "Content-Type=application/json" `
        --body "@$apiBodyPath" `
        --only-show-errors `
        --output none
}
finally {
    Remove-Item -LiteralPath $apiBodyPath -Force
}
Write-Verbose "Configured the API scope, app role, and application ID URI."

# Create the API enterprise application used for role assignments and token issuance.
Write-Verbose "Creating the API service principal."
$apiServicePrincipal = az ad sp create `
    --id $apiApplication.appId `
    --only-show-errors `
    --output json | ConvertFrom-Json
Write-Verbose "Created API service principal with object ID '$($apiServicePrincipal.id)'."

$userRoleAssignmentBody = @{
    principalId = $ownerId
    resourceId = $apiServicePrincipal.id
    appRoleId = $roleId
} | ConvertTo-Json

$userRoleAssignmentBodyPath = [System.IO.Path]::GetTempFileName()
$userRoleAssignmentBody | Set-Content -LiteralPath $userRoleAssignmentBodyPath -Encoding utf8NoBOM

Write-Verbose "Assigning the Directory.Generate app role to signed-in user '$($account.user.name)'."
try {
    az rest `
        --method POST `
        --uri "https://graph.microsoft.com/v1.0/users/$ownerId/appRoleAssignments" `
        --headers "Content-Type=application/json" `
        --body "@$userRoleAssignmentBodyPath" `
        --only-show-errors `
        --output none
}
finally {
    Remove-Item -LiteralPath $userRoleAssignmentBodyPath -Force
}
Write-Verbose "Assigned the Directory.Generate app role to the signed-in user."

# Create the public client used by Swagger UI's authorization-code flow with PKCE.
Write-Verbose "Creating Swagger app registration '$SwaggerDisplayName'."
$swaggerApplication = az ad app create `
    --display-name $SwaggerDisplayName `
    --sign-in-audience AzureADMyOrg `
    --only-show-errors `
    --output json | ConvertFrom-Json
Write-Verbose "Created Swagger app registration with client ID '$($swaggerApplication.appId)'."
Add-CurrentUserAsApplicationOwner `
    -ApplicationObjectId $swaggerApplication.id `
    -ApplicationDisplayName $SwaggerDisplayName

# Configure Swagger as a browser-based SPA and grant it the delegated Directory.Access permission.
$swaggerBody = @{
    spa = @{
        redirectUris = @($SwaggerRedirectUri.AbsoluteUri)
    }
    requiredResourceAccess = @(
        @{
            resourceAppId = $apiApplication.appId
            resourceAccess = @(
                @{
                    id = $scopeId
                    type = "Scope"
                }
            )
        }
    )
} | ConvertTo-Json -Depth 10

$swaggerBodyPath = [System.IO.Path]::GetTempFileName()
$swaggerBody | Set-Content -LiteralPath $swaggerBodyPath -Encoding utf8NoBOM

Write-Verbose "Configuring Swagger redirect URI '$($SwaggerRedirectUri.AbsoluteUri)' and delegated API permission."
try {
    az rest `
        --method PATCH `
        --uri "https://graph.microsoft.com/v1.0/applications/$($swaggerApplication.id)" `
        --headers "Content-Type=application/json" `
        --body "@$swaggerBodyPath" `
        --only-show-errors `
        --output none
}
finally {
    Remove-Item -LiteralPath $swaggerBodyPath -Force
}
Write-Verbose "Configured the Swagger SPA and delegated API permission."

# Create the Swagger enterprise application used for tenant consent and permission tracking.
Write-Verbose "Creating the Swagger service principal."
$swaggerServicePrincipal = az ad sp create `
    --id $swaggerApplication.appId `
    --only-show-errors `
    --output json | ConvertFrom-Json
Write-Verbose "Created Swagger service principal with object ID '$($swaggerServicePrincipal.id)'."

# Create the confidential client used to test application access with client credentials.
Write-Verbose "Creating Console app registration '$ConsoleDisplayName'."
$consoleApplication = az ad app create `
    --display-name $ConsoleDisplayName `
    --sign-in-audience AzureADMyOrg `
    --only-show-errors `
    --output json | ConvertFrom-Json
Write-Verbose "Created Console app registration with client ID '$($consoleApplication.appId)'."
Add-CurrentUserAsApplicationOwner `
    -ApplicationObjectId $consoleApplication.id `
    -ApplicationDisplayName $ConsoleDisplayName

# Configure the API app role as an application permission. The service-principal assignment below grants it.
$consoleBody = @{
    requiredResourceAccess = @(
        @{
            resourceAppId = $apiApplication.appId
            resourceAccess = @(
                @{
                    id = $roleId
                    type = "Role"
                }
            )
        }
    )
} | ConvertTo-Json -Depth 10

$consoleBodyPath = [System.IO.Path]::GetTempFileName()
$consoleBody | Set-Content -LiteralPath $consoleBodyPath -Encoding utf8NoBOM

Write-Verbose "Configuring the Directory.Generate application permission for the Console registration."
try {
    az rest `
        --method PATCH `
        --uri "https://graph.microsoft.com/v1.0/applications/$($consoleApplication.id)" `
        --headers "Content-Type=application/json" `
        --body "@$consoleBodyPath" `
        --only-show-errors `
        --output none
}
finally {
    Remove-Item -LiteralPath $consoleBodyPath -Force
}
Write-Verbose "Configured the Console application permission."

Write-Verbose "Creating the Console service principal."
$consoleServicePrincipal = az ad sp create `
    --id $consoleApplication.appId `
    --only-show-errors `
    --output json | ConvertFrom-Json
Write-Verbose "Created Console service principal with object ID '$($consoleServicePrincipal.id)'."

$roleAssignmentBody = @{
    principalId = $consoleServicePrincipal.id
    resourceId = $apiServicePrincipal.id
    appRoleId = $roleId
} | ConvertTo-Json

$roleAssignmentBodyPath = [System.IO.Path]::GetTempFileName()
$roleAssignmentBody | Set-Content -LiteralPath $roleAssignmentBodyPath -Encoding utf8NoBOM

Write-Verbose "Assigning the Directory.Generate app role to the Console service principal."
try {
    az rest `
        --method POST `
        --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$($consoleServicePrincipal.id)/appRoleAssignments" `
        --headers "Content-Type=application/json" `
        --body "@$roleAssignmentBodyPath" `
        --only-show-errors `
        --output none
}
finally {
    Remove-Item -LiteralPath $roleAssignmentBodyPath -Force
}
Write-Verbose "Assigned the Directory.Generate app role to the Console service principal."

# Return separate non-secret configuration documents for the API and Console projects.
Write-Verbose "App registration setup is complete."
Write-Verbose "The Console tool creates and removes its temporary credential when it runs."
$apiAppSettings = [pscustomobject]@{
    AzureAd = [pscustomobject]@{
        Instance = "https://login.microsoftonline.com/"
        TenantId = $tenantId
        ClientId = $apiApplication.appId
    }
    Swagger = [pscustomobject]@{
        ClientId = $swaggerApplication.appId
    }
}

$consoleAppSettings = [pscustomobject]@{
    DirectoryGenerator = [pscustomobject]@{
        ApiBaseUrl = $SwaggerRedirectUri.GetLeftPart([System.UriPartial]::Authority)
        TenantId = $tenantId
        ApiClientId = $apiApplication.appId
        ConsoleClientId = $consoleApplication.appId
        OutputDirectory = "."
    }
}

Write-Output "src/DirectoryGenerator.Api/appsettings.Development.json"
$apiAppSettings | ConvertTo-Json -Depth 3

Write-Output "src/DirectoryGenerator.Console/appsettings.Development.json"
$consoleAppSettings | ConvertTo-Json -Depth 3