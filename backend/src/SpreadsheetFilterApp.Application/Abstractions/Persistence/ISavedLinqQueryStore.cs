using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SpreadsheetFilterApp.Application.Abstractions.Persistence;

public interface ISavedLinqQueryStore
{
    Task<SavedLinqQuery> CreateAsync(string name, string linqCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<SavedLinqQuery>> ListAsync(CancellationToken cancellationToken);
    Task<SavedLinqQuery?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);
}

public sealed class SavedLinqQuery
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required string LinqCode { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
