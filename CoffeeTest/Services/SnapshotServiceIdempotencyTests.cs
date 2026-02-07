using CoffeeApi.DTOs;
using CoffeeApi.Services;
using CoffeeTest.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeTest.Services;

public class SnapshotServiceIdempotencyTests
{
    private static IngestPayloadDto MakePayload(int coffee, int coffeeAndMilk = 0, int milk = 0)
    {
        return new IngestPayloadDto
        {
            Data = new IngestDataDto
            {
                Status = new List<StatusItemDto>
                {
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", Value = coffee },
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", Value = coffeeAndMilk },
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", Value = milk },
                    new() { Key = "BSH.Common.Status.OperationState", Value = "BSH.Common.EnumType.OperationState.Ready" },
                }
            }
        };
    }

    [Fact]
    public async Task ProcessIngest_FirstSnapshot_IsAlwaysCreated()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        var (created, snapshot) = await service.ProcessIngestAsync(MakePayload(10));

        Assert.True(created);
        Assert.Equal(10, snapshot.BeverageCounterCoffee);
    }

    [Fact]
    public async Task ProcessIngest_SameCounters_SkipsDuplicate()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        await service.ProcessIngestAsync(MakePayload(10));
        var (created, _) = await service.ProcessIngestAsync(MakePayload(10));

        Assert.False(created);
        Assert.Single(db.MachineSnapshots);
    }

    [Fact]
    public async Task ProcessIngest_IncreasedCoffee_CreatesNew()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        await service.ProcessIngestAsync(MakePayload(10));
        var (created, _) = await service.ProcessIngestAsync(MakePayload(11));

        Assert.True(created);
        Assert.Equal(2, db.MachineSnapshots.Count());
    }

    [Fact]
    public async Task ProcessIngest_IncreasedMilk_CreatesNew()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        await service.ProcessIngestAsync(MakePayload(10, milk: 5));
        var (created, _) = await service.ProcessIngestAsync(MakePayload(10, milk: 6));

        Assert.True(created);
    }

    [Fact]
    public async Task ProcessIngest_ExtractsOperationState()
    {
        using var db = TestDbContextFactory.Create();
        var service = new SnapshotService(db, NullLogger<SnapshotService>.Instance);

        var (_, snapshot) = await service.ProcessIngestAsync(MakePayload(1));

        Assert.Equal("Ready", snapshot.OperationState);
    }
}
