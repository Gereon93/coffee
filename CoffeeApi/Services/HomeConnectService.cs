using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CoffeeApi.Services;

public class HomeConnectService : IHomeConnectService
{
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

        _logger.LogInformation("Calling n8n power webhook with state={State}", state);

        var response = await _httpClient.PutAsync(_webhookUrl, content);
        response.EnsureSuccessStatusCode();
    }
}
