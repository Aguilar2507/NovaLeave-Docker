using NovaLeave.Domain.ValueObjects;

namespace NovaLeave.Domain.Tests;

public class DateRangeTests
{
    [Fact]
    public void Constructor_startAfterEnd_throws()
    {
        var start = new DateOnly(2026, 8, 10);
        var end = new DateOnly(2026, 8, 5);

        Assert.Throws<ArgumentException>(() => new DateRange(start, end));
    }

    [Fact]
    public void Constructor_sameDay_isAllowed()
    {
        var day = new DateOnly(2026, 8, 10);

        var range = new DateRange(day, day);

        Assert.Equal(1, range.GetDayCount());
    }

    [Fact]
    public void GetDayCount_isInclusive()
    {
        var range = new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5));

        Assert.Equal(5, range.GetDayCount());
    }

    [Theory]
    [InlineData("2026-08-01", "2026-08-05", "2026-08-05", "2026-08-10", true)]  // touch at end
    [InlineData("2026-08-01", "2026-08-05", "2026-08-06", "2026-08-10", false)] // adjacent no overlap
    [InlineData("2026-08-01", "2026-08-10", "2026-08-05", "2026-08-07", true)]  // fully contained
    [InlineData("2026-08-01", "2026-08-05", "2026-07-25", "2026-08-01", true)]  // touch at start
    public void Overlaps_matrix(string aStart, string aEnd, string bStart, string bEnd, bool expected)
    {
        var a = new DateRange(DateOnly.Parse(aStart), DateOnly.Parse(aEnd));
        var b = new DateRange(DateOnly.Parse(bStart), DateOnly.Parse(bEnd));

        Assert.Equal(expected, a.Overlaps(b));
        Assert.Equal(expected, b.Overlaps(a));
    }
}
