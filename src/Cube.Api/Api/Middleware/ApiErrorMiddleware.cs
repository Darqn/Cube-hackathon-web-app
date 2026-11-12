using Microsoft.EntityFrameworkCore;

namespace Cube.Api.Api.Middleware;

internal sealed class ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers.CacheControl = "no-store";

        try
        {
            await next(context);
        }
        catch (Exception error) when (error is ApiError or DbUpdateException or BadHttpRequestException)
        {
            var status = error switch
            {
                ApiError expected => expected.Status,
                DbUpdateException => StatusCodes.Status409Conflict,
                BadHttpRequestException badRequest => badRequest.StatusCode,
                _ => StatusCodes.Status500InternalServerError
            };
            var code = error is ApiError apiError ? apiError.Message
                : error is DbUpdateException ? "database_constraint_or_concurrency_conflict"
                : "invalid_request";

            if (error is DbUpdateException)
                logger.LogWarning("Database operation failed with {ExceptionType}; trace {TraceId}", error.GetType().Name, context.TraceIdentifier);

            // Preserve the existing error contract, including handling after a response has started.
            if (!context.Response.HasStarted)
                await Results.Problem(statusCode: status, title: code,
                    extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier })
                    .ExecuteAsync(context);
        }
    }
}
