using Azure.Core;
using Azure.Identity;
using DirectoryGenerator.Api.Auth;
using DirectoryGenerator.Api.Directory;
using DirectoryGenerator.Api.Directory.Profiles;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.GenerateDirectory, policy =>
        policy.RequireRole(AppRoles.DirectoryGenerate));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IDirectoryProfileCatalog>(_ =>
{
    var configuredPath = builder.Configuration["DirectoryProfiles:Path"];
    if (string.IsNullOrWhiteSpace(configuredPath))
    {
        throw new InvalidOperationException("DirectoryProfiles:Path configuration is required.");
    }

    var profileDirectory = Path.GetFullPath(configuredPath, builder.Environment.ContentRootPath);
    return new FileDirectoryProfileCatalog(profileDirectory);
});
builder.Services.AddSingleton<TokenCredential>(_ =>
    new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        TenantId = builder.Configuration["AzureAd:TenantId"]
    }));
builder.Services.AddSingleton(serviceProvider => new GraphServiceClient(
    serviceProvider.GetRequiredService<TokenCredential>(),
    ["https://graph.microsoft.com/.default"]));
builder.Services.AddSingleton<IDirectoryReader, GraphDirectoryReader>();
builder.Services.AddSingleton<IDirectoryEntryOrganizer, DirectoryEntryOrganizer>();
builder.Services.AddSingleton<IDirectoryTemplateLoader>(serviceProvider =>
    new FileDirectoryTemplateLoader(
        builder.Environment.ContentRootPath,
        serviceProvider.GetRequiredService<IDirectoryProfileCatalog>()));
builder.Services.AddSingleton<IDirectoryDocumentRenderer, OpenXmlDirectoryDocumentRenderer>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSwaggerGen(options =>
{
    var tenantId = builder.Configuration["AzureAd:TenantId"];
    var apiClientId = builder.Configuration["AzureAd:ClientId"];

    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"),
                TokenUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string>
                {
                    [$"api://{apiClientId}/Directory.Access"] = "Access Directory Generator"
                }
            }
        }
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("oauth2", document)] =
            [$"api://{apiClientId}/Directory.Access"]
    });
});

var app = builder.Build();
_ = app.Services.GetRequiredService<IDirectoryProfileCatalog>();
_ = app.Services.GetRequiredService<IDirectoryTemplateLoader>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.OAuthClientId(builder.Configuration["Swagger:ClientId"]);
        options.OAuthUsePkce();
        options.OAuthScopeSeparator(" ");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
