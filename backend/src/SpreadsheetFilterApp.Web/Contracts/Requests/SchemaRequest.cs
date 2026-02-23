using Microsoft.AspNetCore.Http;

namespace SpreadsheetFilterApp.Web.Contracts.Requests;

public sealed class SchemaRequest
{
    public required IFormFile File { get; init; }
}
