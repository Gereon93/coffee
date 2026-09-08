using System.Text.Json;
using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public static class IngestPayloadValidator
{
    public static readonly IReadOnlyList<string> RequiredCupCounterKeys =
    [
        "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee",
        "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk",
        "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk",
        "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups",
    ];

    public static bool TryValidateCupCounters(
        IReadOnlyList<StatusItemDto> status,
        out IReadOnlyList<string> details)
    {
        var errors = new List<string>();

        foreach (var requiredKey in RequiredCupCounterKeys)
        {
            object? counterValue = null;
            var present = false;
            foreach (var item in status)
            {
                if (item.Key == requiredKey)
                {
                    counterValue = item.Value;
                    present = true;
                }
            }

            if (!present)
            {
                errors.Add($"Missing required counter: {requiredKey}");
                continue;
            }

            if (!IsValidNumericValue(counterValue))
            {
                errors.Add($"Invalid numeric value for counter: {requiredKey}");
            }
        }

        details = errors;
        return errors.Count == 0;
    }

    public static bool IsValidNumericValue(object? value) =>
        TryDecodeNonNegativeInt32(value, out _);

    public static bool TryDecodeNonNegativeInt32(object? value, out int decoded)
    {
        decoded = 0;

        switch (value)
        {
            case int i when i >= 0:
                decoded = i;
                return true;
            case long l when l is >= 0 and <= int.MaxValue:
                decoded = (int)l;
                return true;
            case double d when d >= 0 && d <= int.MaxValue && d == Math.Truncate(d):
                decoded = (int)d;
                return true;
            case JsonElement json when json.ValueKind == JsonValueKind.Number:
                if (!json.TryGetInt64(out var raw) || raw is < 0 or > int.MaxValue)
                {
                    return false;
                }

                if (json.TryGetDouble(out var asDouble) && asDouble != Math.Truncate(asDouble))
                {
                    return false;
                }

                decoded = (int)raw;
                return true;
            case string text when int.TryParse(text, out var parsed) && parsed >= 0:
                decoded = parsed;
                return true;
            default:
                return false;
        }
    }
}
