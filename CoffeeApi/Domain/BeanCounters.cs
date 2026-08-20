namespace CoffeeApi.Domain;

/// <summary>
/// The beverage counters that can draw beans, and the hopper each one uses
/// unless a manual correction says otherwise. Milk and hot water never draw
/// beans, so they have no entry here and never appear in a bean usage.
/// </summary>
public static class BeanCounters
{
    /// <summary>Hopper holding the everyday beans. Plain coffee draws from it.</summary>
    public const int PrimaryHopper = 1;

    /// <summary>Hopper holding the espresso beans. Milk drinks draw from it.</summary>
    public const int EspressoHopper = 2;

    /// <summary>Plain coffee. <see cref="PrimaryHopper"/> by default.</summary>
    public const string Coffee = "coffee";

    /// <summary>
    /// Coffee-and-milk drinks (cappuccino, latte macchiato).
    /// <see cref="EspressoHopper"/> by default, because they are pulled with
    /// espresso beans.
    /// </summary>
    public const string CoffeeAndMilk = "coffeeAndMilk";

    /// <summary>Every counter that can draw beans, in reporting order.</summary>
    public static readonly string[] All = [Coffee, CoffeeAndMilk];

    public static bool IsValid(string counter) => Array.IndexOf(All, counter) >= 0;

    /// <summary>
    /// Whether a hopper assignment is one the model knows. <c>null</c> is valid
    /// and means "no bean consumption".
    /// </summary>
    public static bool IsValidHopper(int? hopper) =>
        hopper is null or PrimaryHopper or EspressoHopper;

    /// <summary>
    /// Hopper a counter draws from when nothing was corrected manually.
    /// Anything not explicitly mapped falls back to <see cref="PrimaryHopper"/>.
    /// </summary>
    public static int DefaultHopper(string counter) =>
        counter == CoffeeAndMilk ? EspressoHopper : PrimaryHopper;

    /// <summary>
    /// Drinks drawn on <paramref name="counter"/> between two cumulative
    /// readings. Counters only ever climb, so a negative difference is a
    /// counter reset and reads as zero.
    /// </summary>
    public static int DeltaOf(string counter, MachineSnapshot previous, MachineSnapshot current) => counter switch
    {
        Coffee => Math.Max(0, current.BeverageCounterCoffee - previous.BeverageCounterCoffee),
        CoffeeAndMilk => Math.Max(0, current.BeverageCounterCoffeeAndMilk - previous.BeverageCounterCoffeeAndMilk),
        _ => 0
    };
}
