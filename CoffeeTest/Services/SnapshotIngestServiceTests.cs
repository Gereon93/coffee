using CoffeeApi.DTOs;
using CoffeeApi.Services;
using CoffeeTest.Helpers;

namespace CoffeeTest.Services;

public class SnapshotIngestServiceTests
{
    private static IngestPayloadDto MakePayload(int coffee, int coffeeAndMilk = 0, int milk = 0, int hotWaterCups = 0)
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
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", Value = hotWaterCups },
                    new() { Key = "BSH.Common.Status.OperationState", Value = "BSH.Common.EnumType.OperationState.Ready" },
                }
            }
        };
    }

    [Fact]
    public async Task ProcessIngest_FirstSnapshot_IsAlwaysCreated()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);

        var (created, snapshot) = await service.ProcessIngestAsync(MakePayload(10));

        Assert.True(created);
        Assert.Equal(10, snapshot.BeverageCounterCoffee);
    }

    [Fact]
    public async Task ProcessIngest_SameCounters_SkipsDuplicate()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);

        await service.ProcessIngestAsync(MakePayload(10));
        var (created, _) = await service.ProcessIngestAsync(MakePayload(10));

        Assert.False(created);
        Assert.Single(db.MachineSnapshots);
    }

    [Fact]
    public async Task ProcessIngest_IncreasedCoffee_CreatesNew()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);

        await service.ProcessIngestAsync(MakePayload(10));
        var (created, _) = await service.ProcessIngestAsync(MakePayload(11));

        Assert.True(created);
        Assert.Equal(2, db.MachineSnapshots.Count());
    }

    [Fact]
    public async Task ProcessIngest_IncreasedMilk_CreatesNew()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);

        await service.ProcessIngestAsync(MakePayload(10, milk: 5));
        var (created, _) = await service.ProcessIngestAsync(MakePayload(10, milk: 6));

        Assert.True(created);
    }

    [Fact]
    public async Task ProcessIngest_ExtractsOperationState()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);

        var (_, snapshot) = await service.ProcessIngestAsync(MakePayload(1));

        Assert.Equal("Ready", snapshot.OperationState);
    }

    [Fact]
    public async Task ProcessIngest_CounterReset_CreatesNewSnapshot()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);

        await service.ProcessIngestAsync(MakePayload(10));
        var (created, snapshot) = await service.ProcessIngestAsync(MakePayload(0));

        Assert.True(created);
        Assert.Equal(0, snapshot.BeverageCounterCoffee);
        Assert.Equal(2, db.MachineSnapshots.Count());
    }

    [Fact]
    public async Task ProcessIngest_PartialReset_CreatesNewSnapshot()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);

        await service.ProcessIngestAsync(MakePayload(10, coffeeAndMilk: 5, milk: 2));
        var (created, _) = await service.ProcessIngestAsync(MakePayload(0, coffeeAndMilk: 5, milk: 2));

        Assert.True(created);
        Assert.Equal(2, db.MachineSnapshots.Count());
    }

    [Fact]
    public async Task ProcessIngest_AfterReset_IncreaseUsesNewBaseline()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);

        await service.ProcessIngestAsync(MakePayload(100));
        await service.ProcessIngestAsync(MakePayload(0));
        var (created, snapshot) = await service.ProcessIngestAsync(MakePayload(5));

        Assert.True(created);
        Assert.Equal(5, snapshot.BeverageCounterCoffee);
        Assert.Equal(3, db.MachineSnapshots.Count());
    }

    [Fact]
    public async Task ProcessIngest_IncompleteCupCounters_ThrowsInvalidIngestPayloadException()
    {
        using var db = TestDbContextFactory.Create();
        var service = SnapshotServices.Ingest(db);
        var payload = new IngestPayloadDto
        {
            Data = new IngestDataDto
            {
                Status = new List<StatusItemDto>
                {
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", Value = 1 },
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", Value = 0 },
                    new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", Value = 0 },
                    new() { Key = "BSH.Common.Status.OperationState", Value = "BSH.Common.EnumType.OperationState.Ready" },
                }
            }
        };

        await Assert.ThrowsAsync<InvalidIngestPayloadException>(() => service.ProcessIngestAsync(payload));
    }

}
