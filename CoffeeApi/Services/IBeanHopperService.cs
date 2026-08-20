using CoffeeApi.Domain;
using CoffeeApi.DTOs;

namespace CoffeeApi.Services;

public enum BeanHopperError
{
    None,
    InvalidCounter,
    InvalidHopper,
    SnapshotNotFound,
    NoConsumption,
    OverrideNotFound
}

/// <summary>
/// Assigns bean draws to the EQ900's two hoppers. Counters are cumulative, so a
/// draw only exists as a delta between two consecutive snapshots; the delta is
/// attributed to the later of the two. Kaffee goes to hopper 1 and K+Milch to
/// hopper 2 unless a stored override says otherwise.
/// </summary>
public interface IBeanHopperService
{
    /// <summary>
    /// Bean draws per snapshot for a sequence ordered oldest-first. The first
    /// element only serves as the baseline and gets no entry; snapshots whose
    /// delta contains no bean draw are absent from the result.
    /// </summary>
    Task<Dictionary<int, List<BeanHopperUsageDto>>> GetUsageAsync(IReadOnlyList<MachineSnapshot> sequence);

    /// <summary>
    /// Bean draws of a sequence ordered oldest-first, summed per hopper.
    /// </summary>
    Task<BeanHopperTotalsDto> GetTotalsAsync(IReadOnlyList<MachineSnapshot> sequence);

    /// <summary>
    /// Sums already-resolved bean draws per hopper, so a caller that fetched
    /// usages once can slice them without querying again.
    /// </summary>
    BeanHopperTotalsDto SumUsage(IEnumerable<BeanHopperUsageDto> usages);

    /// <summary>
    /// Correct the hopper of one counter within one snapshot delta.
    /// </summary>
    Task<(bool Success, BeanHopperError Error, string? Detail)> SetOverrideAsync(int snapshotId, SetBeanHopperDto dto);

    /// <summary>
    /// Drop a correction so the delta falls back to the automatic rule.
    /// </summary>
    Task<(bool Success, BeanHopperError Error, string? Detail)> ClearOverrideAsync(int snapshotId, string counter);
}
