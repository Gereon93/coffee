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

    public static bool IsValidNumericValue(object? value)
    {
        return value switch
        {
            int => true,
            long => true,
            double => true,
            JsonElement json when json.ValueKind == JsonValueKind.Number => true,
            string text => int.TryParse(text, out _),
            _ => false
        };
    }
}
