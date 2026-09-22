using NovaLeave.Application.Features.VacationRequest.Contracts;

namespace NovaLeave.Infrastructure.Services;

// Calcula días laborables entre dos fechas inclusivas, excluyendo sábado, domingo y feriados
// (D-001, FR-003). Usa TimeProvider sólo si se necesita hoy como referencia; el cálculo puro
// no depende del reloj.
public sealed class WorkingDayCalculator : IWorkingDayCalculator
{
    private readonly IHolidayCalendar _holidayCalendar;
    private readonly TimeProvider _timeProvider;

    public WorkingDayCalculator(IHolidayCalendar holidayCalendar, TimeProvider timeProvider)
    {
        _holidayCalendar = holidayCalendar ?? throw new ArgumentNullException(nameof(holidayCalendar));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public int Count(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("La fecha de fin debe ser mayor o igual a la fecha de inicio.", nameof(endDate));
        }

        var holidays = new HashSet<DateOnly>(_holidayCalendar.GetHolidaysBetween(startDate, endDate));

        var count = 0;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            if (holidays.Contains(date))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}
