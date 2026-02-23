using Microsoft.AspNetCore.Mvc;

namespace SpreadsheetFilterApp.Web.Middleware;

public sealed class RequestSizeLimitMiddleware(RequestDelegate next)
{
    private const long MaxUploadBytes = 30 * 1024 * 1024;
    private readonly RequestDelegate _next = next;

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/spreadsheets/schema") &&
            context.Request.ContentLength is > MaxUploadBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "File too large",
                Detail = "Max upload size is 30 MB.",
                Status = StatusCodes.Status413PayloadTooLarge
            });
            return;
        }

        await _next(context);
    }
}
