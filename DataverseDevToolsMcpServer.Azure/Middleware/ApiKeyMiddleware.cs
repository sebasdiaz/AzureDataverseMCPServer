namespace DataverseDevToolsMcpServer.Azure.Middleware;

/// <summary>
/// Validates the X-Api-Key header on every incoming request.
/// If McpServer:ApiKey is not configured the check is skipped,
/// allowing unauthenticated access for local development/testing.
/// </summary>
public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly string? _apiKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _apiKey = configuration["McpServer:ApiKey"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // No key configured → open access (dev/test mode)
        if (string.IsNullOrEmpty(_apiKey))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey)
            || providedKey != _apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync(
                "Unauthorized: provide a valid API key in the X-Api-Key header.");
            return;
        }

        await _next(context);
    }
}
