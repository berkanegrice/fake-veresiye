using Microsoft.AspNetCore.Diagnostics;

namespace FakeVeresiye.Api;

/// <summary>
/// Single place for turning exceptions into HTTP responses, replacing the try/catch blocks
/// that used to be repeated in every controller action that could fail on bad input (a
/// malformed upload, an invalid date range, ...).
///
/// <see cref="ArgumentException"/> and <see cref="InvalidOperationException"/> are how the
/// import/statement code signals "the request is bad" (a corrupt .exa, an empty worksheet, a
/// `from` after `to`, ...), so those become 400s with the message. Anything else is treated as
/// a genuine bug: it's logged with full detail and falls through to ASP.NET's default
/// ProblemDetails handling, which returns a generic 500 without leaking internals to the client.
/// </summary>
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(
                exception, "{Method} {Path} rejected: {Message}",
                httpContext.Request.Method, httpContext.Request.Path, exception.Message);

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                new { error = exception.Message }, cancellationToken);
            return true;
        }

        logger.LogError(
            exception, "Unhandled exception processing {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);
        return false;
    }
}
