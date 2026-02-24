using System;
using System.Threading;
using System.Threading.Tasks;
using SpreadsheetFilterApp.Application.Abstractions.Persistence;
using SpreadsheetFilterApp.Application.Abstractions.Scripting;
using SpreadsheetFilterApp.Application.DTOs;

namespace SpreadsheetFilterApp.Application.Features.Validate;

public sealed class ValidateLinqHandler(ITempFileStore tempFileStore, IScriptValidator scriptValidator)
{
    private readonly ITempFileStore _tempFileStore = tempFileStore;
    private readonly IScriptValidator _scriptValidator = scriptValidator;

    public async Task<ValidationResultDto> HandleAsync(ValidateLinqCommand command, CancellationToken cancellationToken)
    {
        var schema = await _tempFileStore.GetSchemaAsync(command.FileToken, cancellationToken)
            ?? throw new InvalidOperationException("Schema not found for this fileToken.");

        return await _scriptValidator.ValidateAsync(schema.Columns, command.LinqCode, cancellationToken);
    }
}
