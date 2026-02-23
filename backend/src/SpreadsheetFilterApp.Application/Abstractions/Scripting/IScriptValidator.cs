using SpreadsheetFilterApp.Application.DTOs;

namespace SpreadsheetFilterApp.Application.Abstractions.Scripting;

public interface IScriptValidator
{
    Task<ValidationResultDto> ValidateAsync(
        IReadOnlyList<ColumnSchemaDto> schema,
        string linqCode,
        CancellationToken cancellationToken);
}
