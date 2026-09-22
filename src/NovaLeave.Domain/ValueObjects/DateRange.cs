namespace NovaLeave.Domain.ValueObjects;

// Value object inmutable para representar un rango de fechas
public sealed record DateRange
{
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }

    public DateRange(DateOnly startDate, DateOnly endDate)
    {
        // Invariante: StartDate <= EndDate
        if (startDate > endDate)
        {
            throw new ArgumentException("La fecha de inicio debe ser menor o igual a la fecha de fin.", nameof(startDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public int GetDayCount()
    {
        return EndDate.DayNumber - StartDate.DayNumber + 1;
    }

    public bool Overlaps(DateRange other)
    {
        return StartDate <= other.EndDate && EndDate >= other.StartDate;
    }
}
