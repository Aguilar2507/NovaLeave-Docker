namespace NovaLeave.Application.Features.VacationRequest.Contracts;

// Calcula la cantidad de días laborables dentro de un rango, excluyendo fines de semana y feriados (D-001, FR-003).
public interface IWorkingDayCalculator
{
    int Count(DateOnly startDate, DateOnly endDate);
}
