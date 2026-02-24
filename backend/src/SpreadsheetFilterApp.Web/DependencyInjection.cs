using Microsoft.Extensions.DependencyInjection;
using SpreadsheetFilterApp.Application;
using SpreadsheetFilterApp.Infrastructure;

namespace SpreadsheetFilterApp.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebComposition(this IServiceCollection services)
    {
        services.AddApplication();
        services.AddInfrastructure();
        return services;
    }
}
