using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SpreadsheetFilterApp.Application.Abstractions.Persistence;
using SpreadsheetFilterApp.Web.Contracts.Requests;
using SpreadsheetFilterApp.Web.Contracts.Responses;

namespace SpreadsheetFilterApp.Web.Controllers;

[ApiController]
[Route("api/saved-queries")]
public sealed class SavedQueriesController(ISavedLinqQueryStore savedLinqQueryStore) : ControllerBase
{
    private readonly ISavedLinqQueryStore _savedLinqQueryStore = savedLinqQueryStore;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SavedQueryResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await _savedLinqQueryStore.ListAsync(cancellationToken);
        return Ok(items.Select(Map).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SavedQueryResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _savedLinqQueryStore.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(Map(item));
    }

    [HttpPost]
    public async Task<ActionResult<SavedQueryResponse>> Create([FromBody] CreateSavedQueryRequest request, CancellationToken cancellationToken)
    {
        var trimmedName = request.Name.Trim();
        if (trimmedName.Length == 0)
        {
            return BadRequest("Name is required.");
        }

        var item = await _savedLinqQueryStore.CreateAsync(trimmedName, request.LinqCode, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, Map(item));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var deleted = await _savedLinqQueryStore.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static SavedQueryResponse Map(SavedLinqQuery item)
    {
        return new SavedQueryResponse
        {
            Id = item.Id,
            Name = item.Name,
            LinqCode = item.LinqCode,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc
        };
    }
}
