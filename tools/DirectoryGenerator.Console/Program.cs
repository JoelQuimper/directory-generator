using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Applications.Item.AddPassword;
using Microsoft.Graph.Applications.Item.RemovePassword;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;

const string configurationSection = "DirectoryGenerator";
const string credentialPrefix = "Directory Generator Console test";

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var apiBaseUrl = GetRequiredUri(configuration, $"{configurationSection}:ApiBaseUrl");
var tenantId = GetRequiredGuid(configuration, $"{configurationSection}:TenantId");
var apiClientId = GetRequiredGuid(configuration, $"{configurationSection}:ApiClientId");
var consoleClientId = GetRequiredGuid(configuration, $"{configurationSection}:ConsoleClientId");
var outputDirectory = GetRequiredDirectoryPath(
    configuration,
    $"{configurationSection}:OutputDirectory");
var credentialName = $"{credentialPrefix} {Guid.NewGuid():N}";
var azureCliCredential = new AzureCliCredential(new AzureCliCredentialOptions
{
    TenantId = tenantId.ToString()
});
var graphClient = new GraphServiceClient(
    azureCliCredential,
    ["https://graph.microsoft.com/.default"]);
string? consoleApplicationObjectId = null;
PasswordCredential? temporaryCredential = null;

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

try
{
    consoleApplicationObjectId = await GetApplicationObjectIdAsync(
        graphClient,
        consoleClientId,
        cancellationSource.Token);

    Console.WriteLine("Creating a temporary Console client credential.");
    temporaryCredential = await CreateCredentialAsync(
        graphClient,
        consoleApplicationObjectId,
        credentialName,
        cancellationSource.Token);

    var application = ConfidentialClientApplicationBuilder
        .Create(consoleClientId.ToString())
        .WithAuthority(AzureCloudInstance.AzurePublic, tenantId.ToString())
        .WithClientSecret(temporaryCredential.SecretText)
        .Build();

    var authenticationResult = await AcquireTokenForClientAsync(
        application,
        $"api://{apiClientId}/.default",
        cancellationSource.Token);

    using var httpClient = new HttpClient
    {
        BaseAddress = apiBaseUrl
    };
    httpClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);

    using var response = await httpClient.PostAsJsonAsync(
        "/api/v1/directories/generate",
        new { profileId = "default", locale = "en-CA" },
        cancellationSource.Token);

    Console.WriteLine(
        $"POST /api/v1/directories/generate -> {(int)response.StatusCode} {response.ReasonPhrase}");
    if (response.IsSuccessStatusCode)
    {
        var responseFileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "directory.docx";
        var fileName = Path.GetFileName(responseFileName.Trim('"'));
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, fileName);
        await using var output = File.Create(outputPath);
        await response.Content.CopyToAsync(output, cancellationSource.Token);
        Console.WriteLine($"Saved generated directory to '{outputPath}'.");
    }
    else
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationSource.Token);
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            Console.Error.WriteLine(responseBody);
        }
    }

    return response.IsSuccessStatusCode ? 0 : 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The test was cancelled.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
finally
{
    if (consoleApplicationObjectId is not null && temporaryCredential?.KeyId is Guid keyId)
    {
        try
        {
            await DeleteCredentialAsync(graphClient, consoleApplicationObjectId, keyId);
            Console.WriteLine("Deleted the temporary Console client credential.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Temporary credential cleanup failed: {exception.Message}");
        }
    }
}

static async Task<string> GetApplicationObjectIdAsync(
    GraphServiceClient graphClient,
    Guid consoleClientId,
    CancellationToken cancellationToken)
{
    var applications = await graphClient.Applications.GetAsync(requestConfiguration =>
    {
        requestConfiguration.QueryParameters.Filter = $"appId eq '{consoleClientId}'";
        requestConfiguration.QueryParameters.Select = ["id"];
    }, cancellationToken);

    return applications?.Value?.SingleOrDefault()?.Id
        ?? throw new InvalidOperationException(
            $"Console application '{consoleClientId}' was not found in tenant configuration.");
}

static async Task<PasswordCredential> CreateCredentialAsync(
    GraphServiceClient graphClient,
    string consoleApplicationObjectId,
    string credentialName,
    CancellationToken cancellationToken)
{
    var requestBody = new AddPasswordPostRequestBody
    {
        PasswordCredential = new PasswordCredential
        {
            DisplayName = credentialName,
            EndDateTime = DateTimeOffset.UtcNow.AddHours(1)
        }
    };

    var credential = await graphClient.Applications[consoleApplicationObjectId]
        .AddPassword
        .PostAsync(requestBody, cancellationToken: cancellationToken);

    if (credential?.KeyId is null || string.IsNullOrWhiteSpace(credential.SecretText))
    {
        throw new InvalidOperationException("Microsoft Graph did not return the temporary credential.");
    }

    return credential;
}

static Task DeleteCredentialAsync(
    GraphServiceClient graphClient,
    string consoleApplicationObjectId,
    Guid keyId)
{
    var requestBody = new RemovePasswordPostRequestBody
    {
        KeyId = keyId
    };

    return graphClient.Applications[consoleApplicationObjectId]
        .RemovePassword
        .PostAsync(requestBody, cancellationToken: CancellationToken.None);
}

static async Task<AuthenticationResult> AcquireTokenForClientAsync(
    IConfidentialClientApplication application,
    string scope,
    CancellationToken cancellationToken)
{
    const int maximumAttempts = 6;

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            return await application
                .AcquireTokenForClient([scope])
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalServiceException exception) when (
            attempt < maximumAttempts &&
            string.Equals(exception.ErrorCode, "invalid_client", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("AADSTS7000215", StringComparison.Ordinal))
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
            Console.WriteLine(
                $"The temporary credential is still propagating; retrying in {delay.TotalSeconds:0} seconds.");
            await Task.Delay(delay, cancellationToken);
        }
    }
}

static Uri GetRequiredUri(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException($"Configuration '{key}' must be an absolute URI.");
    }

    return uri;
}

static Guid GetRequiredGuid(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (!Guid.TryParse(value, out var result) || result == Guid.Empty)
    {
        throw new InvalidOperationException($"Configuration '{key}' must be a non-empty GUID.");
    }

    return result;
}

static string GetRequiredDirectoryPath(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Configuration '{key}' must be a directory path.");
    }

    return Path.GetFullPath(value, Environment.CurrentDirectory);
}
