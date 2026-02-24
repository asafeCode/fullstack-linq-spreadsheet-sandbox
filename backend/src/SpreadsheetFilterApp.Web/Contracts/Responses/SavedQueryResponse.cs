using System;

namespace SpreadsheetFilterApp.Web.Contracts.Responses;

public sealed class SavedQueryResponse
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required string LinqCode { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
