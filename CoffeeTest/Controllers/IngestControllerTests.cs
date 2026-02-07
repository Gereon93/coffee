using CoffeeApi.Controllers;
using CoffeeApi.DTOs;
using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Controllers;

public class IngestControllerTests
{
    private static IngestController CreateController(ISnapshotService service)
    {
        return new IngestController(service, NullLogger<IngestController>.Instance);
    }

    private static SnapshotService CreateService(string? dbName = null)
    {
        return new SnapshotService(
            TestDbContextFactory.Create(dbName),
            NullLogger<SnapshotService>.Instance);
    }

    [Fact]
    public async Task Ingest_NullPayload_ReturnsBadRequest()
    {
        var controller = CreateController(CreateService());

        var result = await controller.Ingest(null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Ingest_EmptyStatus_ReturnsBadRequest()
    {
        var controller = CreateController(CreateService());
        var payload = new IngestPayloadDto
        {
            Data = new IngestDataDto { Status = new List<StatusItemDto>() }
        };

        var result = await controller.Ingest(payload);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Ingest_ValidPayload_Returns201Created()
    {
        var controller = CreateController(CreateService());
        var payload = new IngestPayloadDto
        {
            Data = new IngestDataDto
            {
                Status = new List<StatusItemDto>
                {
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", Value = 100 },
                }
            }
        };

        var result = await controller.Ingest(payload);

        var created = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<IngestResponseDto>(created.Value);
        Assert.True(response.Created);
        Assert.Equal("Snapshot created", response.Message);
    }

    [Fact]
    public async Task Ingest_Duplicate_Returns200Ok()
    {
        var dbName = Guid.NewGuid().ToString();
        var service = CreateService(dbName);
        var controller = CreateController(service);

        var payload = new IngestPayloadDto
        {
            Data = new IngestDataDto
            {
                Status = new List<StatusItemDto>
                {
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", Value = 100 },
                }
            }
        };

        await controller.Ingest(payload);

        // Same payload again - need new service with same DB
        var service2 = new SnapshotService(
            TestDbContextFactory.Create(dbName),
            NullLogger<SnapshotService>.Instance);
        var controller2 = CreateController(service2);
        var result = await controller2.Ingest(payload);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<IngestResponseDto>(ok.Value);
        Assert.False(response.Created);
    }
}
