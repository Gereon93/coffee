using System.Text.Json.Serialization;

namespace CoffeeApi.DTOs;

/// <summary>
/// Bean draw of one counter column within one snapshot delta.
/// </summary>
public class BeanHopperUsageDto
{
    /// <summary>"coffee" or "coffeeAndMilk".</summary>
    public string Counter { get; set; } = string.Empty;

    /// <summary>Drinks drawn on this counter since the previous snapshot.</summary>
    public int Count { get; set; }

    /// <summary>Hopper 1, hopper 2, or <c>null</c> for "no bean consumption".</summary>
    public int? BeanHopper { get; set; }

    /// <summary>"auto" for the default rule, "manual" for a stored override.</summary>
    public string Source { get; set; } = BeanHopperSources.Auto;
}

/// <summary>Values of <see cref="BeanHopperUsageDto.Source"/>.</summary>
public static class BeanHopperSources
{
    public const string Auto = "auto";
    public const string Manual = "manual";
}

/// <summary>
/// Bean draws of a period, split by hopper. <see cref="Excluded"/> holds the
/// drinks a manual correction took out of bean accounting altogether, so the
/// three fields still add up to the bean-capable drinks of the period.
/// </summary>
public class BeanHopperTotalsDto
{
    public int Hopper1 { get; set; }
    public int Hopper2 { get; set; }
    public int Excluded { get; set; }
}

/// <summary>
/// Request body for correcting the hopper of one counter within one snapshot delta.
/// </summary>
public class SetBeanHopperDto
{
    /// <summary>"coffee" or "coffeeAndMilk".</summary>
    [JsonRequired]
    public string Counter { get; set; } = string.Empty;

    /// <summary>
    /// 1, 2, or <c>null</c> for "no bean consumption". Required in the body —
    /// omitting it is rejected rather than silently read as null.
    /// </summary>
    [JsonRequired]
    public int? BeanHopper { get; set; }
}
