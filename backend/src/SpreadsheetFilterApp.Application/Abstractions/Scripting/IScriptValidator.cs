using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SpreadsheetFilterApp.Application.DTOs;

namespace SpreadsheetFilterApp.Application.Abstractions.Scripting;

public interface IScriptValidator
{
    Task<ValidationResultDto> ValidateAsync(
        IReadOnlyList<ColumnSchemaDto> schema,
        string linqCode,
        CancellationToken cancellationToken);
}
