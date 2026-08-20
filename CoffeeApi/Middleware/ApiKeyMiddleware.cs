using System.Security.Cryptography;

namespace CoffeeApi.Middleware;

/// <summary>
/// Middleware for API Key authentication on protected endpoints
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private const string ApiKeyHeaderName = "X-API-Key";

    // Endpoints that require API key authentication. A null method list protects
    // every method; otherwise only the listed ones are protected, so reads on a
    // path whose writes are protected stay open.
    private static readonly ProtectedRoute[] ProtectedRoutes =
    {
        new("/api/ingest"),
        new("/coffee/power", ["POST"]),
        new("/api/stats/marked-days", ["POST", "DELETE"]),
        new("/api/stats/snapshots", ["POST", "DELETE"]),
    };

    private sealed record ProtectedRoute(string PathPrefix, string[]? Methods = null);

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // Check if this is a protected endpoint
        if (!ProtectedRoutes.Any(r => IsMatch(r, path, method)))
        {
            await _next(context);
            return;
        }

        // Get configured API key
        var configuredApiKey = configuration["ApiKey"];

        if (string.IsNullOrEmpty(configuredApiKey))
        {
            _logger.LogWarning("API Key not configured - allowing request (dev mode)");
            await _next(context);
            return;
        }

        // Check for API key in header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKeyValues))
        {
            _logger.LogWarning("API request without API key from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "API key is required. Provide it via X-API-Key header."
            });
            return;
        }

        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(configuredApiKey),
            System.Text.Encoding.UTF8.GetBytes(providedApiKeyValues.ToString())))
        {
            _logger.LogWarning("Invalid API key attempt from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "Invalid API key."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsMatch(ProtectedRoute route, string path, string method)
    {
        if (!path.StartsWith(route.PathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return route.Methods is null
            || route.Methods.Contains(method, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Extension method for adding the API key middleware
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}
