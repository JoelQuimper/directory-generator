[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "High")]
param(
    [Parameter(Mandatory)]
    [ValidatePattern("^[a-z0-9-]+$")]
    [string] $EnvironmentName,

    [string] $SubscriptionId
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$workloadName = "directory-generator"
$resourceGroupName = "rg-$workloadName-$EnvironmentName"
$appServiceName = "app-$workloadName-$EnvironmentName"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $repositoryRoot "src/DirectoryGenerator.Api/DirectoryGenerator.Api.csproj"
$deploymentTimestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss-fff")
$stagingRoot = Join-Path `
    $repositoryRoot `
    ".artifacts/app-deployment-$deploymentTimestamp"
$publishPath = Join-Path $stagingRoot "publish"
$packagePath = Join-Path $stagingRoot "DirectoryGenerator.Api.zip"

if ($null -eq (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI is required. Install it and run 'az login' before using this script."
}

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK is required to publish the application."
}

if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "API project '$projectFile' was not found."
}

$subscriptionArguments = @()
if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $subscriptionArguments = @("--subscription", $SubscriptionId)
}

$target = "App Service '$appServiceName' in resource group '$resourceGroupName'"
if (-not $PSCmdlet.ShouldProcess($target, "Publish and deploy Directory Generator API")) {
    return
}

try {
    New-Item -ItemType Directory -Path $publishPath -Force | Out-Null

    Write-Host "Publishing Directory Generator API."
    & dotnet publish $projectFile `
        --configuration Release `
        --output $publishPath `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Application publish failed with exit code $LASTEXITCODE."
    }

    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $publishPath,
        $packagePath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    Write-Host "Deploying application to $target."
    & az webapp deploy `
        --resource-group $resourceGroupName `
        --name $appServiceName `
        --src-path $packagePath `
        --type zip `
        --clean true `
        --restart true `
        @subscriptionArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Application deployment failed with exit code $LASTEXITCODE."
    }

    Write-Host "Application deployment completed."
    Write-Host "Application URL: https://$appServiceName.azurewebsites.net"
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
