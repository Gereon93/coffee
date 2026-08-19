using System.Text.Json;
using CoffeeApi.DTOs;
using CoffeeApi.Services;

namespace CoffeeTest.Services;

public class SnapshotPayloadMapperTests
{
    private static readonly DateTime IngestedAt = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private static IngestPayloadDto Payload(params (string Key, object Value)[] status)
    {
        return new IngestPayloadDto
        {
            Data = new IngestDataDto
            {
                Status = status
                    .Select(item => new StatusItemDto { Key = item.Key, Value = item.Value })
                    .ToList()
            }
        };
    }

    private static JsonElement Json(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    [Fact]
    public void Map_StampsTheSuppliedTimestamp()
    {
        var snapshot = SnapshotPayloadMapper.Map(Payload(), IngestedAt);

        Assert.Equal(IngestedAt, snapshot.Timestamp);
        Assert.Equal(IngestedAt, snapshot.CreatedAt);
    }

    [Fact]
    public void Map_OperationState_KeepsOnlyTheLastSegment()
    {
        var snapshot = SnapshotPayloadMapper.Map(
            Payload(("BSH.Common.Status.OperationState", "BSH.Common.EnumType.OperationState.Ready")),
            IngestedAt);

        Assert.Equal("Ready", snapshot.OperationState);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(42L)]
    [InlineData(42.0)]
    [InlineData("42")]
    public void Map_CounterFromScalar_IsConvertedToInt(object value)
    {
        var snapshot = SnapshotPayloadMapper.Map(
            Payload(("ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", value)),
            IngestedAt);

        Assert.Equal(42, snapshot.BeverageCounterCoffee);
    }

    [Fact]
    public void Map_CounterFromJsonNumber_IsConvertedToInt()
    {
        var snapshot = SnapshotPayloadMapper.Map(
            Payload(("ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", Json("7"))),
            IngestedAt);

        Assert.Equal(7, snapshot.BeverageCounterMilk);
    }

    [Fact]
    public void Map_UnparsableCounter_FallsBackToZero()
    {
        var snapshot = SnapshotPayloadMapper.Map(
            Payload(("ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", "n/a")),
            IngestedAt);

        Assert.Equal(0, snapshot.BeverageCounterCoffee);
    }

    [Fact]
    public void Map_BooleanFromJsonElement_IsConverted()
    {
        var snapshot = SnapshotPayloadMapper.Map(
            Payload(
                ("BSH.Common.Status.RemoteControlStartAllowed", Json("true")),
                ("BSH.Common.Status.LocalControlActive", Json("false"))),
            IngestedAt);

        Assert.True(snapshot.RemoteControlAllowed);
        Assert.False(snapshot.LocalControlActive);
    }

    [Fact]
    public void Map_BooleanFromString_IsConverted()
    {
        var snapshot = SnapshotPayloadMapper.Map(
            Payload(("BSH.Common.Status.InteriorIlluminationActive", "true")),
            IngestedAt);

        Assert.True(snapshot.InteriorIlluminationActive);
    }

    [Fact]
    public void Map_UnknownKey_IsIgnored()
    {
        var snapshot = SnapshotPayloadMapper.Map(
            Payload(("BSH.Common.Status.SomethingElse", 99)),
            IngestedAt);

        Assert.Equal(0, snapshot.TotalBeverages);
    }

    [Fact]
    public void Map_AllCounters_AddUpToTotalBeverages()
    {
        var snapshot = SnapshotPayloadMapper.Map(
            Payload(
                ("ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", 10),
                ("ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", 3),
                ("ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", 2),
                ("ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", 1),
                ("ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater", 5)),
            IngestedAt);

        Assert.Equal(16, snapshot.TotalBeverages);
        Assert.Equal(5, snapshot.BeverageCounterHotWater);
    }
}
