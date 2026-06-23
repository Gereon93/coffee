using CoffeeApi.DTOs;
using CoffeeApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CoffeeApi.Controllers;

/// <summary>
/// GET /coffee/status — live status of the EQ900 via n8n -> HomeConnect.
/// Cached 7s to spare BSH quota.
/// </summary>
[ApiController]
[Route("coffee")]
public class CoffeeStatusController : ControllerBase
{
    private const string CacheKey = "coffee:status";
    private static readonly TimeSpan BshApiCacheTtl = TimeSpan.FromSeconds(7);

    private readonly IHomeConnectService _homeConnect;
    private readonly IMemoryCache _cache;

    public CoffeeStatusController(IHomeConnectService homeConnect, IMemoryCache cache)
    {
        _homeConnect = homeConnect;
        _cache = cache;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(CoffeeStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        if (_cache.TryGetValue(CacheKey, out CoffeeStatusDto? cached) && cached != null)
        {
            return Ok(cached);
        }

        var fresh = await _homeConnect.GetStatusAsync();
        _cache.Set(CacheKey, fresh, BshApiCacheTtl);
        return Ok(fresh);
    }
}
