namespace SpreadsheetFilterApp.Infrastructure.Storage;

public sealed class SavedLinqQueryStoreOptions
{
    public const string SectionName = "SavedQueries";

    public string SqlitePath { get; init; } = "data/saved-queries.db";
}
