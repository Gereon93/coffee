namespace CoffeeApi.Domain;

/// <summary>
/// A manual correction of the bean hopper a snapshot delta was drawn from.
/// Keyed by (snapshot, counter) so a mixed delta — two Kaffee plus one K+Milch
/// in the same interval — stays correctable per counter column instead of
/// collapsing onto one hopper. No row means the automatic rule applies.
/// </summary>
public class BeanHopperOverride
{
    /// <summary>The snapshot the delta ends at. Part of the composite key.</summary>
    public int SnapshotId { get; set; }

    /// <summary>Counter column the delta belongs to. See <see cref="BeanCounters"/>.</summary>
    public string Counter { get; set; } = string.Empty;

    /// <summary>Hopper 1, hopper 2, or <c>null</c> for "no bean consumption".</summary>
    public int? BeanHopper { get; set; }

    /// <summary>When the current value was last set. Refreshed on overwrite.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
