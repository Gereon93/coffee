using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public partial class HomeConnectService : IHomeConnectService
{
    private static readonly JsonSerializerOptions StatusJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<HomeConnectService> _logger;
    private readonly string _webhookUrl;

    public HomeConnectService(HttpClient httpClient, ILogger<HomeConnectService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _webhookUrl = configuration["N8n:PowerWebhookUrl"]
            ?? throw new InvalidOperationException("N8n:PowerWebhookUrl not configured");

        var user = configuration["N8n:BasicAuthUser"];
        var password = configuration["N8n:BasicAuthPassword"];
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    public async Task SetPowerStateAsync(bool on)
    {
        var state = on ? "on" : "off";
        var body = JsonSerializer.Serialize(new { state });
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        LogCallingPowerWebhook(state);

        var response = await _httpClient.PutAsync(_webhookUrl, content);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CoffeeStatusDto> GetStatusAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync(_webhookUrl, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                _logger.LogWarning("n8n status webhook returned {Status}", statusCode);
                return Unreachable($"Status-Service antwortete mit {statusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            var parsed = JsonSerializer.Deserialize<CoffeeStatusDto>(body, StatusJsonOptions);

            if (parsed == null)
            {
                return Unreachable("Status-Antwort konnte nicht geparsed werden");
            }

            return parsed;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "n8n status webhook timed out");
            return Unreachable("Status-Service antwortet nicht (Timeout)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "n8n status webhook failed");
            return Unreachable("Status-Service nicht erreichbar");
        }
    }

    private static CoffeeStatusDto Unreachable(string message) => new()
    {
        Status = "ok",
        Reachable = false,
        PowerState = null,
        OperationState = null,
        Label = "Offline",
        LastUpdated = DateTime.UtcNow,
        Message = message
    };

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Calling n8n power webhook with state={State}")]
    private partial void LogCallingPowerWebhook(string state);
}
