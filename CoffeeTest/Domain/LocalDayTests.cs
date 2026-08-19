using CoffeeApi.Domain;

namespace CoffeeTest.Domain;

public class LocalDayTests
{
    [Fact]
    public void BoundsUtc_WithoutOffset_IsMidnightToMidnight()
    {
        var (start, end) = LocalDay.BoundsUtc(new DateOnly(2026, 2, 7), 0);

        Assert.Equal(new DateTime(2026, 2, 7, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 2, 8, 0, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void BoundsUtc_WithCestOffset_ShiftsWindowBackwards()
    {
        var (start, end) = LocalDay.BoundsUtc(new DateOnly(2026, 8, 15), 120);

        Assert.Equal(new DateTime(2026, 8, 14, 22, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 8, 15, 22, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void BoundsUtc_EndIsExclusiveAndExactlyOneDayLong()
    {
        var (start, end) = LocalDay.BoundsUtc(new DateOnly(2026, 2, 7), 60);

        Assert.Equal(TimeSpan.FromDays(1), end - start);
    }

    [Fact]
    public void DateOf_LateEveningUtc_BelongsToNextLocalDay()
    {
        var date = LocalDay.DateOf(new DateTime(2026, 2, 7, 23, 30, 0, DateTimeKind.Utc), 60);

        Assert.Equal(new DateOnly(2026, 2, 8), date);
    }

    [Fact]
    public void ToLocal_AddsTheOffset()
    {
        var local = LocalDay.ToLocal(new DateTime(2026, 2, 7, 14, 0, 0, DateTimeKind.Utc), 60);

        Assert.Equal(15, local.Hour);
    }
}
