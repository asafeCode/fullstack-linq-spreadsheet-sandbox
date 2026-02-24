using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SpreadsheetFilterApp.Web.QueryRuntime;

public interface IQueryWorkQueue
{
    ValueTask EnqueueAsync(QueryWorkItem item, CancellationToken cancellationToken);
    ValueTask<QueryWorkItem> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class QueryWorkQueue : IQueryWorkQueue
{
    private readonly Channel<QueryWorkItem> _channel = Channel.CreateUnbounded<QueryWorkItem>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public ValueTask EnqueueAsync(QueryWorkItem item, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(item, cancellationToken);

    public ValueTask<QueryWorkItem> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}

public enum QueryWorkKind
{
    ParseUpload,
    ExecuteQuery
}

public sealed class QueryWorkItem
{
    public required QueryWorkKind Kind { get; init; }
    public required string JobId { get; init; }
    public string? QueryId { get; init; }
    public QueryExecuteRequest? Query { get; init; }
}
