using Microsoft.Extensions.DependencyInjection;
using SpreadsheetFilterApp.Application.Features.Query;
using SpreadsheetFilterApp.Application.Features.Schema;
using SpreadsheetFilterApp.Application.Features.Validate;

namespace SpreadsheetFilterApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetSchemaHandler>();
        services.AddScoped<RunLinqQueryHandler>();
        services.AddScoped<ValidateLinqHandler>();
        return services;
    }
}
