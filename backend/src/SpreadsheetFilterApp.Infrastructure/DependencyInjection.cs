using Microsoft.Extensions.DependencyInjection;
using SpreadsheetFilterApp.Application.Abstractions.Persistence;
using SpreadsheetFilterApp.Application.Abstractions.Scripting;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using SpreadsheetFilterApp.Application.Abstractions.Time;
using SpreadsheetFilterApp.Domain.Services;
using SpreadsheetFilterApp.Infrastructure.Normalization;
using SpreadsheetFilterApp.Infrastructure.Scripting.Roslyn;
using SpreadsheetFilterApp.Infrastructure.Spreadsheet.Csv;
using SpreadsheetFilterApp.Infrastructure.Spreadsheet.Inference;
using SpreadsheetFilterApp.Infrastructure.Spreadsheet.Xlsx;
using SpreadsheetFilterApp.Infrastructure.Storage;

namespace SpreadsheetFilterApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IColumnNameNormalizer, ColumnNameNormalizer>();
        services.AddSingleton<IColumnTypeInferer, ColumnTypeInferer>();

        services.AddSingleton<ISpreadsheetReader, CsvSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetReader, XlsxSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetWriter, CsvSpreadsheetWriter>();
        services.AddSingleton<ISpreadsheetWriter, XlsxSpreadsheetWriter>();

        services.AddSingleton<ScriptOptionsFactory>();
        services.AddSingleton<SandboxSecurityPolicy>();
        services.AddSingleton<ILinqSandbox, RoslynLinqSandbox>();
        services.AddSingleton<IScriptValidator, RoslynScriptValidator>();

        services.AddSingleton<ITempFileStore, TempFileStore>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
