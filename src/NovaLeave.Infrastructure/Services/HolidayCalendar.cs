using Microsoft.Extensions.Options;
using NovaLeave.Application.Features.VacationRequest.Contracts;

namespace NovaLeave.Infrastructure.Services;

// Opciones de configuración para feriados (leídos desde appsettings.json en "Holidays").
public sealed class HolidayCalendarOptions
{
    public const string SectionName = "Holidays";

    // Lista de feriados en formato "yyyy-MM-dd".
    public IReadOnlyList<string> Dates { get; init; } = Array.Empty<string>();
}

// Implementación de IHolidayCalendar que lee la lista configurada de feriados.
public sealed class HolidayCalendar : IHolidayCalendar
{
    private readonly HashSet<DateOnly> _holidays;

    public HolidayCalendar(IOptions<HolidayCalendarOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _holidays = new HashSet<DateOnly>();
        foreach (var raw in options.Value.Dates)
        {
            if (DateOnly.TryParse(raw, out var parsed))
            {
                _holidays.Add(parsed);
            }
        }
    }

    public bool IsHoliday(DateOnly date) => _holidays.Contains(date);

    public IReadOnlyCollection<DateOnly> GetHolidaysBetween(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            return Array.Empty<DateOnly>();
        }

        return _holidays
            .Where(h => h >= startDate && h <= endDate)
            .ToList();
    }
}
