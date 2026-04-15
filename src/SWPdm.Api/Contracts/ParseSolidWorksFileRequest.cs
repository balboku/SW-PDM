namespace SWPdm.Api.Contracts;

public sealed record ParseSolidWorksFileRequest(
    string FilePath,
    string[]? AdditionalSearchPaths);
