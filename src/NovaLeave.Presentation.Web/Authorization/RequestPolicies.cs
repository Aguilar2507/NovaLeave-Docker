using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Presentation.Web.Authorization;

// Nombres canónicos de las políticas de autorización relacionadas con VacationRequest (SR-007, FR-012).
// Registrar en Program.cs con AddAuthorization + AddScoped<IAuthorizationHandler, ...>.
public static class RequestPolicies
{
    public const string OwnsRequest = "OwnsRequest";
    public const string IsAssignedApprover = "IsAssignedApprover";
    public const string NotSelfApproval = "NotSelfApproval";
}

// Requerimiento: el usuario actual es el dueño (OwnerId) de la VacationRequest evaluada.
public sealed class OwnsRequestRequirement : IAuthorizationRequirement;

// Requerimiento: el usuario actual es el approver asignado del dueño de la solicitud.
public sealed class IsAssignedApproverRequirement : IAuthorizationRequirement;

// Requerimiento: el usuario actual NO es el dueño de la solicitud (bloquea self-approval).
public sealed class NotSelfApprovalRequirement : IAuthorizationRequirement;

// Handler resource-based: espera que el resource sea una tupla (VacationRequest, Employee owner, Employee? approver).
// Como las políticas se evalúan sobre distintos recursos, cada handler documenta su forma.
public sealed class OwnsRequestHandler : AuthorizationHandler<OwnsRequestRequirement, VacationRequest>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnsRequestRequirement requirement,
        VacationRequest resource)
    {
        var employeeIdClaim = context.User.FindFirst("EmployeeId")?.Value;
        if (Guid.TryParse(employeeIdClaim, out var employeeId) && resource.OwnerId == employeeId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Espera resource: tupla (VacationRequest solicitud, Employee dueño). Comprueba dueño.AssignedApproverId == usuario actual.
public sealed class IsAssignedApproverHandler
    : AuthorizationHandler<IsAssignedApproverRequirement, (VacationRequest Request, Employee Owner)>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsAssignedApproverRequirement requirement,
        (VacationRequest Request, Employee Owner) resource)
    {
        var employeeIdClaim = context.User.FindFirst("EmployeeId")?.Value;
        if (Guid.TryParse(employeeIdClaim, out var employeeId)
            && resource.Owner.AssignedApproverId.HasValue
            && resource.Owner.AssignedApproverId.Value == employeeId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Bloquea que un empleado apruebe/rechace su propia solicitud (defense-in-depth, complementa AssignedApprover).
public sealed class NotSelfApprovalHandler : AuthorizationHandler<NotSelfApprovalRequirement, VacationRequest>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        NotSelfApprovalRequirement requirement,
        VacationRequest resource)
    {
        var employeeIdClaim = context.User.FindFirst("EmployeeId")?.Value;
        if (Guid.TryParse(employeeIdClaim, out var employeeId) && resource.OwnerId != employeeId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
