using SpreadsheetFilterApp.Application.Features.Query;
using SpreadsheetFilterApp.Application.Features.Schema;
using SpreadsheetFilterApp.Application.Features.Validate;
using SpreadsheetFilterApp.Web.Contracts.Requests;
using SpreadsheetFilterApp.Web.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace SpreadsheetFilterApp.Web.Controllers;

[ApiController]
[Route("api/spreadsheets")]
public sealed class SpreadsheetsController(
    GetSchemaHandler getSchemaHandler,
    RunLinqQueryHandler runLinqQueryHandler,
    ValidateLinqHandler validateLinqHandler) : ControllerBase
{
    private readonly GetSchemaHandler _getSchemaHandler = getSchemaHandler;
    private readonly RunLinqQueryHandler _runLinqQueryHandler = runLinqQueryHandler;
    private readonly ValidateLinqHandler _validateLinqHandler = validateLinqHandler;

    [HttpPost("schema")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<ActionResult<SchemaResponse>> GetSchema([FromForm] SchemaRequest request, CancellationToken cancellationToken)
    {
        if (request.File.Length == 0)
        {
            return BadRequest("File is required.");
        }

        await using var stream = request.File.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        var dto = await _getSchemaHandler.HandleAsync(new GetSchemaCommand
        {
            FileName = request.File.FileName,
            Content = ms.ToArray()
        }, cancellationToken);

        return Ok(new SchemaResponse
        {
            FileToken = dto.FileToken,
            Columns = dto.Columns.Select(x => new ColumnResponse
            {
                OriginalName = x.OriginalName,
                NormalizedName = x.NormalizedName,
                InferredType = x.InferredType
            }).ToList(),
            Preview = new PreviewResponse
            {
                Rows = dto.Preview.Rows,
                RowCountPreview = dto.Preview.RowCountPreview
            }
        });
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ValidateResponse>> Validate([FromBody] ValidateRequest request, CancellationToken cancellationToken)
    {
        var dto = await _validateLinqHandler.HandleAsync(new ValidateLinqCommand
        {
            FileToken = request.FileToken,
            LinqCode = request.LinqCode
        }, cancellationToken);

        return Ok(new ValidateResponse
        {
            Diagnostics = dto.Diagnostics.Select(x => new DiagnosticResponse
            {
                Message = x.Message,
                Line = x.Line,
                Column = x.Column,
                Severity = x.Severity
            }).ToList()
        });
    }

    [HttpPost("query/preview")]
    public async Task<ActionResult<object>> QueryPreview([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        var dto = await _runLinqQueryHandler.HandleAsync(new RunLinqQueryCommand
        {
            FileToken = request.FileToken,
            LinqCode = request.LinqCode,
            OutputFormat = request.OutputFormat,
            GenerateFile = false
        }, cancellationToken);

        var rows = dto.PreviewRows.Select(row => row.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty)).ToList();
        return Ok(new
        {
            rows,
            rowCountPreview = dto.RowCountPreview,
            elapsedMs = dto.ElapsedMs
        });
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        var dto = await _runLinqQueryHandler.HandleAsync(new RunLinqQueryCommand
        {
            FileToken = request.FileToken,
            LinqCode = request.LinqCode,
            OutputFormat = request.OutputFormat
        }, cancellationToken);

        return File(dto.Content, dto.ContentType, dto.FileName);
    }
}
