using System.Text.Json;
using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public static class IngestPayloadValidator
{
    private static readonly IReadOnlyList<string> RequiredCupCounterKeys =
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

    private static bool TryDecodeNonNegativeInt32(object? value, out int decoded)
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
            case double d when d is >= 0 and <= int.MaxValue && double.IsInteger(d):
                decoded = (int)d;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } json
                when json.TryGetInt32(out var int32Value) && int32Value >= 0:
                decoded = int32Value;
                return true;
            case string text when int.TryParse(text, out var parsed) && parsed >= 0:
                decoded = parsed;
                return true;
            default:
                return false;
        }
    }
}
