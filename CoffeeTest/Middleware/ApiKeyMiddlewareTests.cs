using CoffeeApi.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Middleware;

public class ApiKeyMiddlewareTests
{
    private const string ConfiguredKey = "test-api-key";

    [Fact]
    public async Task ProtectedEndpoint_WithNoApiKeyConfigured_InProduction_Returns503()
    {
        var context = CreateContext(environmentName: Environments.Production);
        context.Request.Path = "/api/ingest";
        context.Request.Method = HttpMethods.Post;

        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithNoApiKeyConfigured_InDevelopment_PassesThrough()
    {
        var context = CreateContext(environmentName: Environments.Development);
        context.Request.Path = "/api/ingest";
        context.Request.Method = HttpMethods.Post;

        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMissingApiKeyHeader_Returns401()
    {
        var context = CreateContext(environmentName: Environments.Production);
        context.Request.Path = "/api/ingest";
        context.Request.Method = HttpMethods.Post;

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, CreateConfiguration(apiKey: ConfiguredKey));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidApiKey_Returns401()
    {
        var context = CreateContext(environmentName: Environments.Production);
        context.Request.Path = "/api/ingest";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers["X-API-Key"] = "wrong-key";

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, CreateConfiguration(apiKey: ConfiguredKey));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidApiKey_PassesThrough()
    {
        var context = CreateContext(environmentName: Environments.Production);
        context.Request.Path = "/api/ingest";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers["X-API-Key"] = ConfiguredKey;

        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration(apiKey: ConfiguredKey));

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnprotectedEndpoint_IgnoresApiKeyCheck()
    {
        var context = CreateContext(environmentName: Environments.Production);
        context.Request.Path = "/api/health";
        context.Request.Method = HttpMethods.Get;

        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreateConfiguration());

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithWhitespaceApiKeyConfigured_IsTreatedAsNotConfigured()
    {
        var context = CreateContext(environmentName: Environments.Production);
        context.Request.Path = "/api/ingest";
        context.Request.Method = HttpMethods.Post;

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, CreateConfiguration(apiKey: "   "));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    private static ApiKeyMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<ApiKeyMiddleware>.Instance);

    private static DefaultHttpContext CreateContext(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment
        {
            EnvironmentName = environmentName,
        });

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
    }

    private static IConfiguration CreateConfiguration(string? apiKey = null)
    {
        var configuration = new ConfigurationManager();
        if (apiKey is not null)
        {
            configuration["ApiKey"] = apiKey;
        }

        return configuration;
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CoffeeTest";
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
