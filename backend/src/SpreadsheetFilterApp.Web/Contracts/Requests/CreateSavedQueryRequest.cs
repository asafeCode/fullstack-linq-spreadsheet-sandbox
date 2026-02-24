using System.ComponentModel.DataAnnotations;

namespace SpreadsheetFilterApp.Web.Contracts.Requests;

public sealed class CreateSavedQueryRequest
{
    [Required]
    [StringLength(200)]
    public required string Name { get; init; }

    [Required]
    public required string LinqCode { get; init; }
}
