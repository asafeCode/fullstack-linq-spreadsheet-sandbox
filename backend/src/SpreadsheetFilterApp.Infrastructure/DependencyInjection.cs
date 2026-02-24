using Microsoft.Extensions.Configuration;
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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
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

        if (configuration is not null)
        {
            services.Configure<SavedLinqQueryStoreOptions>(configuration.GetSection(SavedLinqQueryStoreOptions.SectionName));
        }
        else
        {
            services.Configure<SavedLinqQueryStoreOptions>(_ => { });
        }

        services.AddSingleton<ISavedLinqQueryStore, SqliteSavedLinqQueryStore>();

        return services;
    }
}
