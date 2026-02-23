using Microsoft.AspNetCore.Mvc;

namespace SpreadsheetFilterApp.Web.QueryRuntime;

[ApiController]
[Route("api/query")]
public sealed class QueryRuntimeController(IQueryJobService jobs) : ControllerBase
{
    private readonly IQueryJobService _jobs = jobs;

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(90 * 1024 * 1024)]
    public async Task<ActionResult<object>> Upload(
        [FromForm] IFormFile file1,
        [FromForm] IFormFile? file2,
        [FromForm] IFormFile? file3,
        CancellationToken cancellationToken)
    {
        var jobId = await _jobs.CreateUploadJobAsync(file1, file2, file3, cancellationToken);
        return Ok(new { jobId });
    }

    [HttpGet("status/{jobId}")]
    public ActionResult<UploadJobState> Status([FromRoute] string jobId)
    {
        var state = _jobs.GetUploadJob(jobId);
        return state is null ? NotFound() : Ok(state);
    }

    [HttpGet("{jobId}/preview/{sheetName}")]
    public ActionResult<PreviewPageResponse> PreviewPage(
        [FromRoute] string jobId,
        [FromRoute] string sheetName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200)
    {
        var response = _jobs.GetPreviewPage(jobId, sheetName, page, pageSize);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{jobId}/execute")]
    public async Task<ActionResult<object>> Execute([FromRoute] string jobId, [FromBody] QueryExecuteRequest request, CancellationToken cancellationToken)
    {
        var queryId = await _jobs.EnqueueQueryAsync(jobId, request, cancellationToken);
        return Ok(new { queryId });
    }

    [HttpPost("{jobId}/unify")]
    public async Task<ActionResult<ContractSheetInfo>> Unify(
        [FromRoute] string jobId,
        [FromBody] UnifySheetsRequest request,
        CancellationToken cancellationToken)
    {
        var unified = await _jobs.UnifySheetsAsync(jobId, request, cancellationToken);
        return Ok(unified);
    }

    [HttpGet("execute/{queryId}")]
    public ActionResult<QueryRunState> QueryStatus([FromRoute] string queryId)
    {
        var state = _jobs.GetQueryRun(queryId);
        return state is null ? NotFound() : Ok(state);
    }

    [HttpGet("contract")]
    public ActionResult<QueryContractResponse> Contract([FromQuery] string? jobId)
    {
        return Ok(_jobs.GetContract(jobId));
    }
}
