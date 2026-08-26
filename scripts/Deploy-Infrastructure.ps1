[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param(
    [string] $Location = "canadacentral",

    [string] $SubscriptionId,

    [string] $TemplateFile = "infra/main.bicep",

    [string] $ParametersFile = "infra/main.bicepparam"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true
$deploymentName = "directory-generator-infrastructure-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedTemplateFile = [System.IO.Path]::GetFullPath($TemplateFile, $repositoryRoot)
$resolvedParametersFile = [System.IO.Path]::GetFullPath($ParametersFile, $repositoryRoot)

if (-not (Test-Path -LiteralPath $resolvedTemplateFile -PathType Leaf)) {
    throw "Bicep template '$resolvedTemplateFile' was not found."
}

if (-not (Test-Path -LiteralPath $resolvedParametersFile -PathType Leaf)) {
    throw "Bicep parameters file '$resolvedParametersFile' was not found."
}

if (Select-String -LiteralPath $resolvedParametersFile -Pattern "<[^>]+>" -Quiet) {
    throw "Replace the placeholder values in '$resolvedParametersFile' before deploying."
}

$subscriptionArguments = @()
if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $subscriptionArguments = @("--subscription", $SubscriptionId)
}

$deploymentArguments = @(
    "--name", $deploymentName,
    "--location", $Location,
    "--template-file", $resolvedTemplateFile,
    "--parameters", $resolvedParametersFile
) + $subscriptionArguments

$target = if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    "the active Azure subscription"
}
else {
    "Azure subscription '$SubscriptionId'"
}

if (-not $PSCmdlet.ShouldProcess($target, "Deploy infrastructure using '$resolvedParametersFile'")) {
    return
}

Write-Host "Deploying infrastructure to $target."
$deploymentJson = & az deployment sub create @deploymentArguments --output json
if ($LASTEXITCODE -ne 0) {
    throw "Infrastructure deployment failed with exit code $LASTEXITCODE."
}

$deployment = $deploymentJson | ConvertFrom-Json
$outputs = $deployment.properties.outputs
$managedIdentityPrincipalId = $outputs.managedIdentityPrincipalId.value

Write-Host "Azure infrastructure deployment completed."
Write-Host "Resource group: $($outputs.resourceGroupName.value)"
Write-Host "App Service plan: $($outputs.appServicePlanName.value)"
Write-Host "App Service: $($outputs.appServiceName.value)"
Write-Host "Application URL: $($outputs.appServiceUrl.value)"

Write-Host "Checking Microsoft Graph User.Read.All application permission to the App Service managed identity."
$graphApplicationId = "00000003-0000-0000-c000-000000000000"
$graphServicePrincipal = & az ad sp show --id $graphApplicationId --output json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) {
    throw "Failed to find the Microsoft Graph service principal."
}

$userReadAllRole = $graphServicePrincipal.appRoles | Where-Object {
    $_.value -eq "User.Read.All" -and
    $_.isEnabled -and
    $_.allowedMemberTypes -contains "Application"
} | Select-Object -First 1

if ($null -eq $userReadAllRole) {
    throw "Microsoft Graph application permission User.Read.All was not found."
}

$currentAssignments = & az rest `
    --method GET `
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$managedIdentityPrincipalId/appRoleAssignments" `
    --output json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) {
    throw "Failed to read Microsoft Graph permissions for managed identity '$managedIdentityPrincipalId'."
}

$hasUserReadAll = $currentAssignments.value | Where-Object {
    $_.resourceId -eq $graphServicePrincipal.id -and
    $_.appRoleId -eq $userReadAllRole.id
} | Select-Object -First 1

if ($null -eq $hasUserReadAll) {
    $assignmentBody = @{
        principalId = $managedIdentityPrincipalId
        resourceId = $graphServicePrincipal.id
        appRoleId = $userReadAllRole.id
    } | ConvertTo-Json -Compress

    $assignmentBodyPath = Join-Path `
        $repositoryRoot `
        ".graph-role-assignment-$([guid]::NewGuid().ToString('N')).json"
    $assignmentBody | Set-Content -LiteralPath $assignmentBodyPath -Encoding utf8NoBOM

    try {
        & az rest `
            --method POST `
            --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$managedIdentityPrincipalId/appRoleAssignments" `
            --headers "Content-Type=application/json" `
            --body "@$assignmentBodyPath" `
            --output none
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to grant Microsoft Graph User.Read.All to managed identity '$managedIdentityPrincipalId'."
        }
    }
    finally {
        Remove-Item -LiteralPath $assignmentBodyPath -Force
    }

    Write-Host "Granted Microsoft Graph User.Read.All to the App Service managed identity."
}
else {
    Write-Host "Microsoft Graph User.Read.All is already assigned to the App Service managed identity."
}

Write-Host "Managed identity principal ID: $managedIdentityPrincipalId"
