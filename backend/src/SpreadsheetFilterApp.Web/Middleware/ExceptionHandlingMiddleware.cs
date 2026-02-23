using Microsoft.AspNetCore.Mvc;

namespace SpreadsheetFilterApp.Web.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Request canceled",
                Detail = "The request timed out or was canceled.",
                Status = StatusCodes.Status408RequestTimeout
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Request validation failed.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Request failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing request.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "An unexpected server error occurred.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
