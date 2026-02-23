using Microsoft.AspNetCore.SignalR;

namespace SpreadsheetFilterApp.Web.QueryRuntime;

public sealed class QueryProgressHub : Hub
{
    public Task JoinJob(string jobId) => Groups.AddToGroupAsync(Context.ConnectionId, $"job:{jobId}");

    public Task JoinQuery(string queryId) => Groups.AddToGroupAsync(Context.ConnectionId, $"query:{queryId}");
}
