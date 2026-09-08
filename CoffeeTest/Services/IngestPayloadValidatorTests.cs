using System.Text.Json;
using CoffeeApi.DTOs;
using CoffeeApi.Services;

namespace CoffeeTest.Services;

public class IngestPayloadValidatorTests
{
    private static List<StatusItemDto> FullCounters(int coffee = 1) =>
    [
        new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee", Value = coffee },
        new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk", Value = 0 },
        new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk", Value = 0 },
        new() { Key = "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups", Value = 0 },
    ];

    [Fact]
    public void TryValidateCupCounters_MissingCounter_ReturnsFalseWithDetails()
    {
        var status = FullCounters();
        status.RemoveAt(status.Count - 1);

        var valid = IngestPayloadValidator.TryValidateCupCounters(status, out var details);

        Assert.False(valid);
        Assert.Contains(details, d => d.Contains("HotWaterCups", StringComparison.Ordinal));
    }

    [Fact]
    public void TryValidateCupCounters_InvalidValue_ReturnsFalseWithDetails()
    {
        var status = FullCounters();
        status[0].Value = "not-a-number";

        var valid = IngestPayloadValidator.TryValidateCupCounters(status, out var details);

        Assert.False(valid);
        Assert.Contains(details, d => d.Contains("Invalid numeric value", StringComparison.Ordinal));
    }

    [Fact]
    public void TryValidateCupCounters_ValidPayload_ReturnsTrue()
    {
        var valid = IngestPayloadValidator.TryValidateCupCounters(FullCounters(), out var details);

        Assert.True(valid);
        Assert.Empty(details);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1L)]
    [InlineData(1.0)]
    [InlineData("42")]
    public void IsValidNumericValue_AcceptsSupportedShapes(object value)
    {
        Assert.True(IngestPayloadValidator.IsValidNumericValue(value));
    }

    [Fact]
    public void IsValidNumericValue_AcceptsJsonElementNumber()
    {
        using var document = JsonDocument.Parse("7");
        Assert.True(IngestPayloadValidator.IsValidNumericValue(document.RootElement));
    }
}
