using SpreadsheetFilterApp.Application;
using SpreadsheetFilterApp.Infrastructure;
using SpreadsheetFilterApp.Web.Config;
using SpreadsheetFilterApp.Web.Filters;
using SpreadsheetFilterApp.Web.Middleware;
using SpreadsheetFilterApp.Web.QueryRuntime;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.Configure<QueryRuntimeOptions>(builder.Configuration.GetSection("QueryRuntime"));
builder.Services.AddSingleton<IQueryWorkQueue, QueryWorkQueue>();
builder.Services.AddSingleton<IQueryJobService, QueryJobService>();
builder.Services.AddSingleton<IQuerySandboxProcessClient, QuerySandboxProcessClient>();
builder.Services.AddHostedService<QueryWorkerService>();
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddSwaggerDocumentation();
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidateModelFilter>();
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestSizeLimitMiddleware>();
app.UseCorsPolicy();
app.UseSwaggerDocumentation();
app.MapControllers();
app.MapHub<QueryProgressHub>("/hubs/query-progress");

app.Run();

public partial class Program;
