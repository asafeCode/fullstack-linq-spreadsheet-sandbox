namespace SpreadsheetFilterApp.Web.Config;

public static class CorsConfig
{
    private const string PolicyName = "frontend";

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
            ["http://localhost:5173", "https://localhost:5173"];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, builder =>
            {
                builder.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            });
        });

        return services;
    }

    public static IApplicationBuilder UseCorsPolicy(this WebApplication app)
    {
        app.UseCors(PolicyName);
        return app;
    }
}
