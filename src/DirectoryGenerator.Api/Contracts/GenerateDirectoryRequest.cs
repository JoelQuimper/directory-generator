using System.ComponentModel;

namespace DirectoryGenerator.Api.Contracts;

public sealed record GenerateDirectoryRequest(
	[property: DefaultValue("default")] string ProfileId,
	[property: DefaultValue("en-CA")] string Locale);