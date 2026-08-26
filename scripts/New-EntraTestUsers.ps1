[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "Medium")]
param(
    [Parameter(Mandatory)]
    [ValidatePattern("^[A-Za-z0-9](?:[A-Za-z0-9.-]{0,251}[A-Za-z0-9])?$")]
    [string] $TenantDomain
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$departments = @(
    "Core",
    "Test",
    "Accounting",
    "Education Leadership",
    "Facilities",
    "Finance",
    "Human Resources",
    "Information Technology",
    "Operations",
    "Payroll"
)

$jobTitles = @(
    "Administrative Assistant",
    "Coordinator",
    "Manager",
    "Senior Specialist",
    "Analyst",
    "Director"
)

function New-RandomPassword {
    $randomText = [Convert]::ToBase64String(
        [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(18))
    return "Dg!7aA$randomText"
}

function Invoke-GraphRequest {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("GET", "POST")]
        [string] $Method,

        [Parameter(Mandatory)]
        [string] $Uri,

        [object] $Body,

        [Parameter(Mandatory)]
        [hashtable] $Headers
    )

    $maximumAttempts = 5
    for ($attempt = 1; ; $attempt++) {
        try {
            $parameters = @{
                Method = $Method
                Uri = $Uri
                Headers = $Headers
            }

            if ($null -ne $Body) {
                $parameters.ContentType = "application/json"
                $parameters.Body = $Body | ConvertTo-Json -Depth 5 -Compress
            }

            return Invoke-RestMethod @parameters
        }
        catch {
            $statusCode = [int] $_.Exception.Response.StatusCode
            $isTransient = $statusCode -eq 429 -or $statusCode -ge 500
            if (-not $isTransient -or $attempt -ge $maximumAttempts) {
                throw
            }

            $delay = [Math]::Min([Math]::Pow(2, $attempt - 1), 16)
            Write-Warning "Microsoft Graph returned HTTP $statusCode. Retrying in $delay seconds."
            Start-Sleep -Seconds $delay
        }
    }
}

$normalizedDomain = $TenantDomain.Trim().ToLowerInvariant()
$users = for ($departmentIndex = 0; $departmentIndex -lt $departments.Count; $departmentIndex++) {
    for ($userIndex = 0; $userIndex -lt 6; $userIndex++) {
        $number = ($departmentIndex * 6) + $userIndex + 1
        $givenName = "Test$($number.ToString('D2'))"
        $surname = "User$($number.ToString('D2'))"
        $mailNickname = "dg-test-$($number.ToString('D3'))"

        [pscustomobject]@{
            AccountEnabled = $true
            DisplayName = "$givenName $surname"
            GivenName = $givenName
            Surname = $surname
            UserPrincipalName = "$mailNickname@$normalizedDomain"
            MailNickname = $mailNickname
            Department = $departments[$departmentIndex]
            JobTitle = $jobTitles[$userIndex]
            BusinessPhones = @("$((5500 + $number).ToString())")
        }
    }
}

if ($WhatIfPreference) {
    foreach ($user in $users) {
        $PSCmdlet.ShouldProcess(
            $user.UserPrincipalName,
            "Create Microsoft Entra test user in department '$($user.Department)'") | Out-Null
    }

    Write-Output "WhatIf: 60 users across 10 departments; no Azure calls were made."
    return
}

Write-Verbose "Reading the current Azure CLI context."
$account = az account show --only-show-errors --output json | ConvertFrom-Json
if ($account.user.type -ne "user") {
    throw "This script requires an interactive Azure CLI user login."
}

if (-not [string]::Equals($account.tenantId, $account.homeTenantId, [StringComparison]::OrdinalIgnoreCase)) {
    Write-Warning "The selected subscription tenant differs from the signed-in user's home tenant. Verify that '$normalizedDomain' belongs to tenant '$($account.tenantId)'."
}

Write-Verbose "Acquiring a Microsoft Graph token for tenant '$($account.tenantId)'."
$token = az account get-access-token `
    --tenant $account.tenantId `
    --resource-type ms-graph `
    --only-show-errors `
    --output json | ConvertFrom-Json
$headers = @{
    Authorization = "Bearer $($token.accessToken)"
}

$created = 0
$skipped = 0
foreach ($user in $users) {
    $escapedUpn = $user.UserPrincipalName.Replace("'", "''", [StringComparison]::Ordinal)
    $filter = [Uri]::EscapeDataString("userPrincipalName eq '$escapedUpn'")
    $lookupUri = "https://graph.microsoft.com/v1.0/users?`$filter=$filter&`$select=id"
    $existing = Invoke-GraphRequest -Method GET -Uri $lookupUri -Headers $headers

    if ($existing.value.Count -gt 0) {
        Write-Verbose "Skipping existing test user '$($user.UserPrincipalName)'."
        $skipped++
        continue
    }

    if (-not $PSCmdlet.ShouldProcess(
        $user.UserPrincipalName,
        "Create Microsoft Entra test user in department '$($user.Department)'")) {
        continue
    }

    $body = @{
        accountEnabled = $user.AccountEnabled
        displayName = $user.DisplayName
        givenName = $user.GivenName
        surname = $user.Surname
        userPrincipalName = $user.UserPrincipalName
        mailNickname = $user.MailNickname
        department = $user.Department
        jobTitle = $user.JobTitle
        businessPhones = $user.BusinessPhones
        passwordProfile = @{
            forceChangePasswordNextSignIn = $true
            password = New-RandomPassword
        }
    }

    try {
        Invoke-GraphRequest `
            -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/users" `
            -Body $body `
            -Headers $headers | Out-Null
    }
    catch {
        if ([int] $_.Exception.Response.StatusCode -eq 403) {
            throw "Creating users requires Microsoft Graph User.ReadWrite.All or Directory.ReadWrite.All and an appropriate Microsoft Entra role. $($_.Exception.Message)"
        }

        throw
    }

    Write-Verbose "Created '$($user.UserPrincipalName)' in '$($user.Department)'."
    $created++
}

Write-Output "Created: $created; skipped existing: $skipped; departments: $($departments.Count)."