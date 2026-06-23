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

    // Endpoints that require API key authentication
    private static readonly string[] ProtectedPaths = { "/api/ingest" };

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Check if this is a protected endpoint
        if (!ProtectedPaths.Any(p => path.StartsWith(p.ToLower())))
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
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKeyValues)
            || providedApiKeyValues.ToString() is not string providedApiKey)
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
            System.Text.Encoding.UTF8.GetBytes(providedApiKey)))
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
