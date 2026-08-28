using DirectoryGenerator.Api.Contracts;
using DirectoryGenerator.Api.Directory.Organizing;
using DirectoryGenerator.Api.Directory.Profiles;

namespace DirectoryGenerator.Api.Tests.Directory;

public sealed class DirectoryEntryOrganizerTests
{
    [Fact]
    public void GroupsAndSortsEntriesUsingProfileRules()
    {
        var sort = new DirectorySort
        {
            GroupBy = new DirectorySortRule
            {
                Property = "department",
                Direction = "ascending"
            },
            Entries =
            [
                new DirectorySortRule { Property = "surname", Direction = "ascending" },
                new DirectorySortRule { Property = "givenName", Direction = "descending" }
            ]
        };
        var coreZulu = CreateEntry("Core", "Zulu", "Amy");
        var testAlphaBob = CreateEntry("Test", "Alpha", "Bob");
        var coreAlpha = CreateEntry("Core", "Alpha", "Zoe");
        var testAlphaZoe = CreateEntry("Test", "Alpha", "Zoe");
        var organizer = new DirectoryEntryOrganizer();

        var groups = organizer.Organize(
            [coreZulu, testAlphaBob, coreAlpha, testAlphaZoe],
            sort,
            "en-CA");

        Assert.Equal(["Core", "Test"], groups.Select(group => group.Group));
        Assert.Equal([coreAlpha, coreZulu], groups[0].Entries);
        Assert.Equal([testAlphaZoe, testAlphaBob], groups[1].Entries);
    }

    [Fact]
    public void ReturnsSingleGroupWhenGroupingIsNotConfigured()
    {
        var first = CreateEntry("Core", "Zulu", "Amy");
        var second = CreateEntry("Test", "Alpha", "Zoe");
        var organizer = new DirectoryEntryOrganizer();

        var groups = organizer.Organize([first, second], null, "en-CA");

        var group = Assert.Single(groups);
        Assert.Null(group.Group);
        Assert.Equal([first, second], group.Entries);
    }

    private static DirectoryEntryPreview CreateEntry(
        string department,
        string surname,
        string givenName) =>
        new(
            Guid.NewGuid().ToString(),
            $"{givenName} {surname}",
            givenName,
            surname,
            null,
            department,
            null,
            null,
            [],
            null,
            null,
            null,
            true,
            "Member",
            null);
}