using CoffeeApi.Domain;
using CoffeeApi.DTOs;
using CoffeeApi.Infrastructure;
using CoffeeTest.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeTest.Integration;

public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.CoffeeApiFactory>
{
    private const string ApiKey = "integration-test-key";

    public sealed class CoffeeApiFactory : WebApplicationFactory<CoffeeApi.Program>
    {
        private readonly string _dbPath =
            Path.Combine(Path.GetTempPath(), $"coffee-it-{Guid.NewGuid():N}.db");

        public string DbPath => _dbPath;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.UseSetting("ApiKey", ApiKey);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteOutageHelper.RestoreAccessAndDelete(_dbPath);
        }
    }

    private readonly CoffeeApiFactory _factory;

    public ApiIntegrationTests(CoffeeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_PostStartupSqliteOutage_ReportsDisconnected()
    {
        await using var factory = new CoffeeApiFactory();
        var client = factory.CreateClient();

        var healthy = await client.GetAsync("/api/health");
        healthy.EnsureSuccessStatusCode();
        using (var baseline = JsonDocument.Parse(await healthy.Content.ReadAsStringAsync()))
        {
            Assert.Equal(HealthResponseDto.Connected, baseline.RootElement.GetProperty("database").GetString());
        }

        SqliteOutageHelper.DenyAccess(factory.DbPath);

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HealthResponseDto.Disconnected, document.RootElement.GetProperty("database").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("lastSnapshot").ValueKind);
    }

    [Fact]
    public async Task GetStats_OnFreshDatabase_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_MachineIdQueryScopesTheSqliteRead()
    {
        await using var factory = new CoffeeApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MachineSnapshots.AddRange(
                new MachineSnapshot
                {
                    MachineId = "EQ900-A",
                    Timestamp = new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc),
                    BeverageCounterCoffee = 10,
                    OperationState = "Ready"
                },
                new MachineSnapshot
                {
                    MachineId = "EQ900-B",
                    Timestamp = new DateTime(2026, 2, 7, 9, 0, 0, DateTimeKind.Utc),
                    BeverageCounterCoffee = 20,
                    OperationState = "Ready"
                });
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClient().GetAsync(
            "/api/stats?machineId=EQ900-A&pageSize=100");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = document.RootElement.GetProperty("data").EnumerateArray().ToArray();

        var row = Assert.Single(rows);
        Assert.Equal("Ready", row.GetProperty("operationState").GetString());
        Assert.Equal(10, row.GetProperty("beverageCounterCoffee").GetInt32());
    }

    [Fact]
    public async Task GetDailyStats_MachineIdQueryScopesTheSqliteRead()
    {
        await using var factory = new CoffeeApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MachineSnapshots.AddRange(
                new MachineSnapshot
                {
                    MachineId = "EQ900-B",
                    Timestamp = new DateTime(2026, 2, 6, 22, 0, 0, DateTimeKind.Utc),
                    BeverageCounterCoffee = 200,
                    OperationState = "Ready"
                },
                new MachineSnapshot
                {
                    MachineId = "EQ900-A",
                    Timestamp = new DateTime(2026, 2, 6, 23, 0, 0, DateTimeKind.Utc),
                    BeverageCounterCoffee = 999,
                    OperationState = "Ready"
                },
                new MachineSnapshot
                {
                    MachineId = "EQ900-A",
                    Timestamp = new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc),
                    BeverageCounterCoffee = 100,
                    OperationState = "Ready"
                },
                new MachineSnapshot
                {
                    MachineId = "EQ900-A",
                    Timestamp = new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc),
                    BeverageCounterCoffee = 101,
                    OperationState = "Ready"
                },
                new MachineSnapshot
                {
                    MachineId = "EQ900-B",
                    Timestamp = new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc),
                    BeverageCounterCoffee = 200,
                    OperationState = "Ready"
                },
                new MachineSnapshot
                {
                    MachineId = "EQ900-B",
                    Timestamp = new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc),
                    BeverageCounterCoffee = 205,
                    OperationState = "Ready"
                });
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClient().GetAsync(
            "/api/stats/daily/2026-02-07?machineId=EQ900-B");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = document.RootElement.GetProperty("summary");

        Assert.Equal(5, summary.GetProperty("coffeeToday").GetInt32());
        Assert.Equal(5, summary.GetProperty("totalToday").GetInt32());

        var snapshots = document.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();
        Assert.Equal(3, snapshots.Length);
        Assert.Equal(200, snapshots[0].GetProperty("beverageCounterCoffee").GetInt32());
        Assert.Equal(200, snapshots[1].GetProperty("beverageCounterCoffee").GetInt32());
        Assert.Equal(205, snapshots[2].GetProperty("beverageCounterCoffee").GetInt32());
    }

    [Fact]
    public async Task GetRangeStats_MachineIdQueryScopesTheSqliteRead()
    {
        await using var factory = new CoffeeApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var requestedMachineBaseline = new MachineSnapshot
            {
                MachineId = "EQ900-B",
                Timestamp = new DateTime(2026, 2, 5, 23, 0, 0, DateTimeKind.Utc),
                BeverageCounterCoffee = 10,
                OperationState = "Ready"
            };
            var requestedMachineFeb6 = new MachineSnapshot
            {
                MachineId = "EQ900-B",
                Timestamp = new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc),
                BeverageCounterCoffee = 13,
                OperationState = "Ready"
            };
            var requestedMachineFeb7 = new MachineSnapshot
            {
                MachineId = "EQ900-B",
                Timestamp = new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc),
                BeverageCounterCoffee = 17,
                OperationState = "Ready"
            };
            var interferingMachineBaseline = new MachineSnapshot
            {
                MachineId = "EQ900-A",
                Timestamp = new DateTime(2026, 2, 5, 23, 30, 0, DateTimeKind.Utc),
                BeverageCounterCoffee = 500,
                OperationState = "Ready"
            };
            var interferingMachineFeb6 = new MachineSnapshot
            {
                MachineId = "EQ900-A",
                Timestamp = new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc),
                BeverageCounterCoffee = 200,
                OperationState = "Ready"
            };
            var interferingMachineFeb7 = new MachineSnapshot
            {
                MachineId = "EQ900-A",
                Timestamp = new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc),
                BeverageCounterCoffee = 300,
                OperationState = "Ready"
            };
            db.MachineSnapshots.AddRange(
                requestedMachineBaseline,
                interferingMachineBaseline,
                requestedMachineFeb6,
                requestedMachineFeb7,
                interferingMachineFeb6,
                interferingMachineFeb7);
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClient().GetAsync(
            "/api/stats/range?from=2026-02-06&to=2026-02-07&machineId=EQ900-B");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data").EnumerateArray().ToArray();

        Assert.Equal(2, data.Length);
        Assert.Equal("2026-02-06", data[0].GetProperty("date").GetString());
        Assert.Equal(3, data[0].GetProperty("coffeeCount").GetInt32());
        Assert.Equal("2026-02-07", data[1].GetProperty("date").GetString());
        Assert.Equal(4, data[1].GetProperty("coffeeCount").GetInt32());
    }

    [Fact]
    public async Task GetHeatmap_MachineIdQueryScopesTheSqliteRead()
    {
        await using var factory = new CoffeeApiFactory();
        var monday = DateTime.UtcNow.Date;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var interferingMachineMondayMorning = new MachineSnapshot
            {
                MachineId = "EQ900-A",
                Timestamp = monday.AddHours(10),
                BeverageCounterCoffee = 100,
                OperationState = "Ready"
            };
            var interferingMachineMondayLater = new MachineSnapshot
            {
                MachineId = "EQ900-A",
                Timestamp = monday.AddHours(11),
                BeverageCounterCoffee = 102,
                OperationState = "Ready"
            };
            var requestedMachineMondayMorning = new MachineSnapshot
            {
                MachineId = "EQ900-B",
                Timestamp = monday.AddHours(10),
                BeverageCounterCoffee = 10,
                OperationState = "Ready"
            };
            var requestedMachineMondayLater = new MachineSnapshot
            {
                MachineId = "EQ900-B",
                Timestamp = monday.AddHours(11),
                BeverageCounterCoffee = 15,
                OperationState = "Ready"
            };
            db.MachineSnapshots.AddRange(
                interferingMachineMondayMorning,
                interferingMachineMondayLater,
                requestedMachineMondayMorning,
                requestedMachineMondayLater);
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClient().GetAsync(
            "/api/stats/heatmap?machineId=EQ900-B");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var heatmap = document.RootElement.GetProperty("heatmap").EnumerateArray().ToArray();

        var point = Assert.Single(heatmap);
        Assert.Equal(1, point.GetProperty("dayOfWeek").GetInt32());
        Assert.Equal(11, point.GetProperty("hour").GetInt32());
        Assert.Equal(5, point.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Ingest_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/ingest",
            new StringContent("""{"data":{"status":[]}}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HistoricalBackfill_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/admin/historical-backfill/preview",
            new StringContent("{\"commissionedAt\":\"2025-07-10\"}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HistoricalBackfill_WithApiKeyPersistsRowsAndRejectsSecondApply()
    {
        const string machineId = "EQ900-INTEGRATION";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MachineSnapshots.Add(new MachineSnapshot
            {
                MachineId = machineId,
                Timestamp = new DateTime(2026, 1, 25, 16, 10, 0, DateTimeKind.Utc),
                BeverageCounterCoffee = 988,
                BeverageCounterCoffeeAndMilk = 10,
                BeverageCounterMilk = 11,
                BeverageCounterHotWaterCups = 1,
                BeverageCounterHotWater = 150,
                OperationState = "Ready"
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var requestBody = JsonSerializer.Serialize(new
        {
            commissionedAt = "2025-07-10",
            machineId
        });

        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/admin/historical-backfill/apply");
        firstRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        firstRequest.Headers.Add("X-API-Key", ApiKey);

        var firstResponse = await client.SendAsync(firstRequest);
        firstResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(175, await db.MachineSnapshots.CountAsync(snapshot =>
                snapshot.IsEstimated && snapshot.MachineId == machineId));
        }

        using var secondRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/admin/historical-backfill/apply");
        secondRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        secondRequest.Headers.Add("X-API-Key", ApiKey);

        var secondResponse = await client.SendAsync(secondRequest);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithValidApiKey_PersistsSnapshotAndIsReadableViaStats()
    {
        var client = _factory.CreateClient();
        const string payload =
            """{"data":{"status":[{"key":"ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee","value":42},{"key":"ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk","value":0},{"key":"ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk","value":0},{"key":"ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups","value":0}]}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-API-Key", ApiKey);

        var ingestResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, ingestResponse.StatusCode);

        var statsResponse = await client.GetAsync("/api/stats");
        statsResponse.EnsureSuccessStatusCode();
        var body = await statsResponse.Content.ReadAsStringAsync();
        Assert.Contains("42", body);
    }

    [Fact]
    public async Task Power_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/coffee/power",
            new StringContent("""{"state":"on"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Power_WithValidApiKey_InvalidStateReturnsBadRequestWithoutActuatingWebhook()
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/coffee/power")
        {
            Content = new StringContent("""{"state":"nonsense"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-API-Key", ApiKey);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoffeeStatus_WithoutApiKey_IsNotBlocked()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/coffee/status");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMarkedDay_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/stats/marked-days",
            new StringContent(
                """{"date":"2026-01-05","kind":"mass-import","reason":"holiday"}""",
                Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMarkedDay_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/stats/marked-days/2026-01-05");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMarkedDays_WithoutApiKey_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/stats/marked-days");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateMarkedDay_WithValidApiKey_IsCreatedAndDeletable()
    {
        var client = _factory.CreateClient();
        const string date = "2026-01-06";

        var create = new HttpRequestMessage(HttpMethod.Post, "/api/stats/marked-days")
        {
            Content = new StringContent(
                $$"""{"date":"{{date}}","kind":"mass-import","reason":"integration test"}""",
                Encoding.UTF8,
                "application/json"),
        };
        create.Headers.Add("X-API-Key", ApiKey);

        var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/stats/marked-days/{date}");
        delete.Headers.Add("X-API-Key", ApiKey);

        var deleteResponse = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task SetBeanHopper_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/stats/snapshots/1/bean-hopper",
            new StringContent("""{"counter":"coffee","beanHopper":2}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClearBeanHopper_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/stats/snapshots/1/bean-hopper?counter=coffee");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BeanHopper_OverrideSurvivesTheRoundTripThroughTheReadApi()
    {
        await using var factory = new CoffeeApiFactory();
        var client = factory.CreateClient();

        await IngestCoffeeCounterAsync(client, 500);
        var correctedId = await IngestCoffeeCounterAsync(client, 503);

        var set = new HttpRequestMessage(HttpMethod.Post, $"/api/stats/snapshots/{correctedId}/bean-hopper")
        {
            Content = new StringContent("""{"counter":"coffee","beanHopper":2}""", Encoding.UTF8, "application/json"),
        };
        set.Headers.Add("X-API-Key", ApiKey);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(set)).StatusCode);

        var corrected = await ReadSnapshotAsync(client, correctedId);
        var draw = corrected.GetProperty("beanHoppers").EnumerateArray().Single();
        Assert.Equal("coffee", draw.GetProperty("counter").GetString());
        Assert.Equal(2, draw.GetProperty("beanHopper").GetInt32());
        Assert.Equal("manual", draw.GetProperty("source").GetString());

        var clear = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/stats/snapshots/{correctedId}/bean-hopper?counter=coffee");
        clear.Headers.Add("X-API-Key", ApiKey);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(clear)).StatusCode);

        var restored = await ReadSnapshotAsync(client, correctedId);
        var auto = restored.GetProperty("beanHoppers").EnumerateArray().Single();
        Assert.Equal(2, auto.GetProperty("beanHopper").GetInt32());
        Assert.Equal("auto", auto.GetProperty("source").GetString());
    }

    [Fact]
    public async Task BeanHopper_SingleCupOfCoffee_ReachesTheReadApiAsEspressoDraw()
    {
        await using var factory = new CoffeeApiFactory();
        var client = factory.CreateClient();

        await IngestCoffeeCounterAsync(client, 700);
        var singleCupId = await IngestCoffeeCounterAsync(client, 701);

        var snapshot = await ReadSnapshotAsync(client, singleCupId);
        var draw = snapshot.GetProperty("beanHoppers").EnumerateArray().Single();
        Assert.Equal("coffee", draw.GetProperty("counter").GetString());
        Assert.Equal(1, draw.GetProperty("count").GetInt32());
        Assert.Equal(1, draw.GetProperty("beanHopper").GetInt32());
        Assert.Equal("auto", draw.GetProperty("source").GetString());

        var day = snapshot.GetProperty("timestamp").GetDateTime();
        var dailyResponse = await client.GetAsync($"/api/stats/daily/{day:yyyy-MM-dd}");
        dailyResponse.EnsureSuccessStatusCode();

        using var daily = JsonDocument.Parse(await dailyResponse.Content.ReadAsStringAsync());
        var totals = daily.RootElement.GetProperty("summary").GetProperty("beanHoppers");
        Assert.Equal(1, totals.GetProperty("hopper1").GetInt32());
        Assert.Equal(0, totals.GetProperty("hopper2").GetInt32());
    }

    [Fact]
    public async Task Ingest_InvalidCupCounters_Returns400AndDoesNotPersist()
    {
        await using var factory = new CoffeeApiFactory();
        var client = factory.CreateClient();

        var cases = new (string Name, string Payload)[]
        {
            (
                "missing counter",
                """
                {
                  "data": {
                    "status": [
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "value": 1 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", "value": 0 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", "value": 0 }
                    ]
                  }
                }
                """
            ),
            (
                "negative counter",
                """
                {
                  "data": {
                    "status": [
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "value": -1 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", "value": 0 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", "value": 0 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", "value": 0 }
                    ]
                  }
                }
                """
            ),
            (
                "fractional counter",
                """
                {
                  "data": {
                    "status": [
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "value": 1.5 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", "value": 0 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", "value": 0 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", "value": 0 }
                    ]
                  }
                }
                """
            ),
            (
                "out-of-range counter",
                """
                {
                  "data": {
                    "status": [
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "value": 2147483648 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", "value": 0 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", "value": 0 },
                      { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", "value": 0 }
                    ]
                  }
                }
                """
            ),
        };

        foreach (var (_, payload) in cases)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-API-Key", ApiKey);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await db.MachineSnapshots.CountAsync());
        }
    }

    [Fact]
    public async Task Ingest_CounterReset_PersistsEpochAndDailySummaryUsesNewBaseline()
    {
        await using var factory = new CoffeeApiFactory();
        var client = factory.CreateClient();

        await IngestCupCountersAsync(client, coffee: 100);
        await IngestCupCountersAsync(client, coffee: 0);
        var lastId = await IngestCupCountersAsync(client, coffee: 5);

        var statsResponse = await client.GetAsync("/api/stats?pageSize=100");
        statsResponse.EnsureSuccessStatusCode();
        using (var statsDocument = JsonDocument.Parse(await statsResponse.Content.ReadAsStringAsync()))
        {
            var rows = statsDocument.RootElement.GetProperty("data").EnumerateArray().ToArray();
            Assert.Equal(3, rows.Length);
        }

        var snapshot = await ReadSnapshotAsync(client, lastId);
        var day = snapshot.GetProperty("timestamp").GetDateTime();
        var dailyResponse = await client.GetAsync($"/api/stats/daily/{day:yyyy-MM-dd}");
        dailyResponse.EnsureSuccessStatusCode();

        using var daily = JsonDocument.Parse(await dailyResponse.Content.ReadAsStringAsync());
        Assert.Equal(5, daily.RootElement.GetProperty("summary").GetProperty("coffeeToday").GetInt32());
    }

    private static async Task<int> IngestCupCountersAsync(
        HttpClient client,
        int coffee,
        int coffeeAndMilk = 0,
        int milk = 0,
        int hotWaterCups = 0)
    {
        var payload = $$"""
            {
              "data": {
                "status": [
                  { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "value": {{coffee}} },
                  { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", "value": {{coffeeAndMilk}} },
                  { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", "value": {{milk}} },
                  { "key": "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", "value": {{hotWaterCups}} }
                ]
              }
            }
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-API-Key", ApiKey);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetInt32();
    }

    private static Task<int> IngestCoffeeCounterAsync(HttpClient client, int counter) =>
        IngestCupCountersAsync(client, counter);

    private static async Task<JsonElement> ReadSnapshotAsync(HttpClient client, int snapshotId)
    {
        var response = await client.GetAsync("/api/stats?pageSize=100");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Single(row => row.GetProperty("id").GetInt32() == snapshotId)
            .Clone();
    }
}
