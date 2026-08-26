using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory.Profiles;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace DirectoryGenerator.Api.Directory;

public sealed class GraphDirectoryReader(GraphServiceClient graphClient) : IDirectoryReader
{
    public async Task<IReadOnlyList<DirectoryEntryPreview>> ReadAsync(
        DirectoryProfile profile,
        CancellationToken cancellationToken)
    {
        var firstPage = await graphClient.Users.GetAsync(requestConfiguration =>
        {
            requestConfiguration.QueryParameters.Filter = profile.Filter;
            requestConfiguration.QueryParameters.Select = profile.Properties.ToArray();
            requestConfiguration.QueryParameters.Top = 999;
        }, cancellationToken);

        if (firstPage is null)
        {
            return [];
        }

        var entries = new List<DirectoryEntryPreview>();
        var pageIterator = PageIterator<User, UserCollectionResponse>.CreatePageIterator(
            graphClient,
            firstPage,
            user =>
            {
                entries.Add(Map(user));
                return true;
            });

        await pageIterator.IterateAsync(cancellationToken);
        return entries;
    }

    private static DirectoryEntryPreview Map(User user) =>
        new(
            user.Id,
            user.DisplayName,
            user.GivenName,
            user.Surname,
            user.JobTitle,
            user.Department,
            user.CompanyName,
            user.OfficeLocation,
            user.BusinessPhones ?? [],
            user.MobilePhone,
            user.Mail,
            user.UserPrincipalName,
            user.AccountEnabled,
            user.UserType,
            MapExtensionAttributes(user.OnPremisesExtensionAttributes));

    private static IReadOnlyDictionary<string, string?>? MapExtensionAttributes(
        OnPremisesExtensionAttributes? attributes)
    {
        if (attributes is null)
        {
            return null;
        }

        return new Dictionary<string, string?>
        {
            ["extensionAttribute1"] = attributes.ExtensionAttribute1,
            ["extensionAttribute2"] = attributes.ExtensionAttribute2,
            ["extensionAttribute3"] = attributes.ExtensionAttribute3,
            ["extensionAttribute4"] = attributes.ExtensionAttribute4,
            ["extensionAttribute5"] = attributes.ExtensionAttribute5,
            ["extensionAttribute6"] = attributes.ExtensionAttribute6,
            ["extensionAttribute7"] = attributes.ExtensionAttribute7,
            ["extensionAttribute8"] = attributes.ExtensionAttribute8,
            ["extensionAttribute9"] = attributes.ExtensionAttribute9,
            ["extensionAttribute10"] = attributes.ExtensionAttribute10,
            ["extensionAttribute11"] = attributes.ExtensionAttribute11,
            ["extensionAttribute12"] = attributes.ExtensionAttribute12,
            ["extensionAttribute13"] = attributes.ExtensionAttribute13,
            ["extensionAttribute14"] = attributes.ExtensionAttribute14,
            ["extensionAttribute15"] = attributes.ExtensionAttribute15
        };
    }
}