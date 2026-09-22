using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NovaLeave.Presentation.Web.ViewComponents;

// Role Switcher ViewComponent (CU-202)
// Muestra el selector de rol solo para usuarios con múltiples roles
public class RoleSwitcherViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var user = UserClaimsPrincipal;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        var roles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        // Solo mostrar el switcher si el usuario tiene múltiples roles
        if (roles.Count <= 1)
        {
            return Content(string.Empty);
        }

        var model = new RoleSwitcherViewModel
        {
            AvailableRoles = roles,
            CurrentPath = HttpContext.Request.Path.ToString()
        };

        return View(model);
    }
}

public class RoleSwitcherViewModel
{
    public List<string> AvailableRoles { get; set; } = new();
    public string CurrentPath { get; set; } = string.Empty;

    public string GetActiveRole()
    {
        // Determinar el rol activo basado en la ruta actual
        foreach (var role in AvailableRoles)
        {
            if (IsViewingAs(role))
            {
                return role;
            }
        }

        // Si no se puede determinar por la ruta, usar el primer rol disponible
        // Preferir "User" sobre "Approver" como default
        if (AvailableRoles.Contains("User"))
        {
            return "User";
        }

        return AvailableRoles.FirstOrDefault() ?? "User";
    }

    public string GetRoleLabel(string role)
    {
        return role switch
        {
            "User" => "Empleado",
            "Approver" => "Aprobador",
            _ => role
        };
    }

    public string GetRoleIcon(string role)
    {
        return role switch
        {
            "User" => "👤",
            "Approver" => "✓",
            _ => "🔹"
        };
    }

    public string GetRoleDashboard(string role)
    {
        return role switch
        {
            "User" => "/Employee/Dashboard",
            "Approver" => "/Approver/Dashboard",
            _ => "/"
        };
    }

    // Determina si el usuario está actualmente visualizando como un rol específico
    public bool IsViewingAs(string role)
    {
        if (role == "User")
        {
            return CurrentPath.StartsWith("/Employee", StringComparison.OrdinalIgnoreCase) ||
                   CurrentPath.StartsWith("/EmployeeRequests", StringComparison.OrdinalIgnoreCase);
        }
        if (role == "Approver")
        {
            return CurrentPath.StartsWith("/Approver", StringComparison.OrdinalIgnoreCase) ||
                   CurrentPath.StartsWith("/ApproverRequests", StringComparison.OrdinalIgnoreCase) ||
                   CurrentPath.StartsWith("/approver/requests", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
