namespace NovaLeave.Application.Features.VacationRequest.Contracts;

// Proveedor de feriados configurados; usado por WorkingDayCalculator.
public interface IHolidayCalendar
{
    bool IsHoliday(DateOnly date);

    IReadOnlyCollection<DateOnly> GetHolidaysBetween(DateOnly startDate, DateOnly endDate);
}
