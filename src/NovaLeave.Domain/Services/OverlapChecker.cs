using NovaLeave.Domain.Entities;

namespace NovaLeave.Domain.Services;

// Servicio de dominio que verifica si un rango de fechas solapa con solicitudes existentes
// en estados Pending o Approved. Se usa durante la creación/edición de solicitudes (D-003, FR-004).
public sealed class OverlapChecker
{
    // Devuelve true si el rango (startDate, endDate) se solapa con alguna solicitud
    // en la colección provista que esté en estado Pending o Approved.
    public bool HasOverlap(
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<VacationRequest> existingRequests)
    {
        ArgumentNullException.ThrowIfNull(existingRequests);

        foreach (var existing in existingRequests)
        {
            // Solo consideramos Pending y Approved (las demás son terminales y no bloquean)
            if (existing.Status != RequestStatus.Pending && existing.Status != RequestStatus.Approved)
            {
                continue;
            }

            // Algoritmo de solapamiento inclusivo: los rangos [a,b] y [c,d] se solapan si a <= d && b >= c
            if (startDate <= existing.EndDate && endDate >= existing.StartDate)
            {
                return true;
            }
        }

        return false;
    }
}
