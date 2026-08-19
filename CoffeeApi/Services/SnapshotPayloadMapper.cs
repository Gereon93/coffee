using System.Text.Json;
using CoffeeApi.Domain;
using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

/// <summary>
/// Translates a Home Connect status payload into a <see cref="MachineSnapshot"/>.
/// Pure and free of persistence concerns: the ingest timestamp is supplied by the
/// caller (ADR-009), the mapper only decodes keys and values.
/// </summary>
public static class SnapshotPayloadMapper
{
    public static MachineSnapshot Map(IngestPayloadDto payload, DateTime timestampUtc)
    {
        var snapshot = new MachineSnapshot
        {
            Timestamp = timestampUtc,
            CreatedAt = timestampUtc
        };

        foreach (var item in payload.Data.Status)
        {
            switch (item.Key)
            {
                case "BSH.Common.Status.OperationState":
                    snapshot.OperationState = ExtractOperationState(item.Value?.ToString() ?? "");
                    break;
                case "BSH.Common.Status.RemoteControlStartAllowed":
                    snapshot.RemoteControlAllowed = ConvertToBool(item.Value);
                    break;
                case "BSH.Common.Status.LocalControlActive":
                    snapshot.LocalControlActive = ConvertToBool(item.Value);
                    break;
                case "BSH.Common.Status.InteriorIlluminationActive":
                    snapshot.InteriorIlluminationActive = ConvertToBool(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffee":
                    snapshot.BeverageCounterCoffee = ConvertToInt(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterCoffeeAndMilk":
                    snapshot.BeverageCounterCoffeeAndMilk = ConvertToInt(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterMilk":
                    snapshot.BeverageCounterMilk = ConvertToInt(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWaterCups":
                    snapshot.BeverageCounterHotWaterCups = ConvertToInt(item.Value);
                    break;
                case "ConsumerProducts.CoffeeMaker.Status.BeverageCounterHotWater":
                    snapshot.BeverageCounterHotWater = ConvertToInt(item.Value);
                    break;
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Reduces a fully qualified Home Connect enum value such as
    /// <c>BSH.Common.EnumType.OperationState.Ready</c> to its last segment.
    /// </summary>
    private static string ExtractOperationState(string fullValue)
    {
        var parts = fullValue.Split('.');
        return parts.Length > 0 ? parts[^1] : "Unknown";
    }

    private static bool ConvertToBool(object? value)
    {
        return value switch
        {
            bool b => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            string s => bool.TryParse(s, out var result) && result,
            _ => false
        };
    }

    private static int ConvertToInt(object? value)
    {
        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            string s => int.TryParse(s, out var result) ? result : 0,
            _ => 0
        };
    }
}
