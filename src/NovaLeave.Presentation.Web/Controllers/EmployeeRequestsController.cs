using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Application.Features.VacationRequest.Cancel;
using NovaLeave.Application.Features.VacationRequest.Create;
using NovaLeave.Application.Features.VacationRequest.Edit;
using NovaLeave.Application.Features.VacationRequest.List;
using NovaLeave.Application.Features.VacationRequest.Void;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Presentation.Web.ViewModels;

namespace NovaLeave.Presentation.Web.Controllers;

// T115: Controller para solicitudes de vacaciones (Employee-owned actions: Create, Index, Detail, Edit, Cancel, Void).
[Authorize(Roles = "User")]
public class EmployeeRequestsController : Controller
{
    private readonly CreateRequestHandler _createHandler;
    private readonly CancelRequestHandler _cancelHandler;
    private readonly VoidRequestHandler _voidHandler;
    private readonly EditRequestHandler _editHandler;
    private readonly ListMyRequestsHandler _listHandler;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmployeeRequestsController> _logger;

    public EmployeeRequestsController(
        CreateRequestHandler createHandler,
        CancelRequestHandler cancelHandler,
        VoidRequestHandler voidHandler,
        EditRequestHandler editHandler,
        ListMyRequestsHandler listHandler,
        ApplicationDbContext context,
        ILogger<EmployeeRequestsController> logger)
    {
        _createHandler = createHandler ?? throw new ArgumentNullException(nameof(createHandler));
        _cancelHandler = cancelHandler ?? throw new ArgumentNullException(nameof(cancelHandler));
        _voidHandler = voidHandler ?? throw new ArgumentNullException(nameof(voidHandler));
        _editHandler = editHandler ?? throw new ArgumentNullException(nameof(editHandler));
        _listHandler = listHandler ?? throw new ArgumentNullException(nameof(listHandler));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // GET: /EmployeeRequests/Index
    // T512 [P] [US5] List own request history with pagination
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            _logger.LogWarning("Index GET sin EmployeeId en claims.");
            return Unauthorized();
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            _logger.LogError("Empleado {EmployeeId} no encontrado al listar solicitudes.", employeeId);
            return NotFound("Empleado no encontrado.");
        }

        var offset = (page - 1) * pageSize;
        var query = new ListMyRequestsQuery(Limit: pageSize, Offset: offset);

        var result = await _listHandler.Handle(query, employee, HttpContext.RequestAborted);

        var viewModel = new RequestListViewModel
        {
            Requests = result.Requests.Select(r => new RequestSummaryViewModel
            {
                Id = r.Id,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                RequestedDays = r.RequestedDays,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                ExpiryDate = r.ExpiryDate
            }).ToList(),
            CurrentBalance = result.CurrentBalance,
            TotalCount = result.TotalCount,
            PageNumber = page,
            PageSize = pageSize
        };

        return View(viewModel);
    }

    // GET: /EmployeeRequests/Detail/{id}
    // T512 [P] [US5] Detail view for own request (owner-only; 404 on unauthorized - SR-003)
    [HttpGet]
    public async Task<IActionResult> Detail(Guid id)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            _logger.LogWarning("Detail GET sin EmployeeId en claims.");
            return Unauthorized();
        }

        var request = await _context.VacationRequests.FindAsync(id);

        // Existence hiding (SR-003): return 404 if not found OR not owned by current employee
        if (request == null || request.OwnerId != employeeId)
        {
            _logger.LogWarning("Solicitud {RequestId} no encontrada o no pertenece al empleado {EmployeeId}.", id, employeeId);
            return NotFound();
        }

        // Map to detail viewmodel (includes Reason per FR-010)
        var viewModel = new EmployeeRequestDetailViewModel
        {
            Id = request.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            RequestedDays = request.RequestedDays,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            ExpiryDate = request.ExpiryDate,
            ResolvedAt = request.ResolvedAt,
            ResolvedBy = request.ResolvedBy,
            RejectionReason = request.RejectionReason
        };

        // Resolver nombre del aprobador si la solicitud fue resuelta por alguien
        if (request.ResolvedBy.HasValue)
        {
            var resolver = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == request.ResolvedBy.Value);
            viewModel.ResolvedByFullName = resolver?.Name;
        }

        return View(viewModel);
    }

    // GET: /EmployeeRequests/Edit/{id}
    // T603 [Phase 8, FR-009] Edit GET action (owner-only, Pending-only)
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            _logger.LogWarning("Edit GET sin EmployeeId en claims.");
            return Unauthorized();
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            _logger.LogError("Empleado {EmployeeId} no encontrado al editar solicitud.", employeeId);
            return NotFound("Empleado no encontrado.");
        }

        var request = await _context.VacationRequests.FindAsync(id);

        // Existence hiding (SR-003): return 404 if not found OR not owned
        if (request == null || request.OwnerId != employeeId)
        {
            _logger.LogWarning("Solicitud {RequestId} no encontrada o no pertenece al empleado {EmployeeId}.", id, employeeId);
            return NotFound();
        }

        // Only Pending requests can be edited (FR-009)
        if (request.Status != Domain.Entities.RequestStatus.Pending)
        {
            TempData["ErrorMessage"] = "Solo las solicitudes en estado Pendiente pueden ser editadas.";
            return RedirectToAction("Detail", new { id });
        }

        var viewModel = new EditRequestViewModel
        {
            Id = request.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            CurrentRequestedDays = request.RequestedDays,
            CurrentBalance = employee.Balance
        };

        return View(viewModel);
    }

    // POST: /EmployeeRequests/Edit/{id}
    // T603 [Phase 8, FR-009] Edit POST action
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditRequestViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            _logger.LogWarning("Edit POST sin EmployeeId en claims.");
            return Unauthorized();
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            _logger.LogError("Empleado {EmployeeId} no encontrado al editar solicitud.", employeeId);
            return NotFound("Empleado no encontrado.");
        }

        if (!ModelState.IsValid)
        {
            model.CurrentBalance = employee.Balance;
            return View(model);
        }

        var command = new NovaLeave.Application.Features.VacationRequest.Edit.EditRequestCommand(
            model.Id,
            model.StartDate,
            model.EndDate,
            model.Reason,
            Guid.NewGuid().ToString());

        try
        {
            await _editHandler.Handle(command, employee, HttpContext.RequestAborted);
            TempData["SuccessMessage"] = "Solicitud editada exitosamente.";
            return RedirectToAction("Detail", new { id = model.Id });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al editar solicitud {RequestId}.", model.Id);
            ModelState.AddModelError(string.Empty, ex.Message);
            model.CurrentBalance = employee.Balance;
            return View(model);
        }
    }

    // GET: /EmployeeRequests/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateRequestViewModel());
    }

    // POST: /EmployeeRequests/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Cargar el Employee desde el claim EmployeeId
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            _logger.LogWarning("Create request POST sin EmployeeId en claims.");
            return Unauthorized();
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee is null)
        {
            _logger.LogWarning("Employee {EmployeeId} no encontrado.", employeeId);
            return NotFound();
        }

        // Obtener correlation-id desde HttpContext.Items (colocado por el middleware en Program.cs)
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N");

        try
        {
            var command = new CreateRequestCommand(
                OwnerId: employeeId,
                StartDate: model.StartDate!.Value,
                EndDate: model.EndDate!.Value,
                Reason: model.Reason ?? string.Empty,
                CorrelationId: correlationId);

            var createdRequest = await _createHandler.Handle(command, employee, HttpContext.RequestAborted);

            _logger.LogInformation(
                "Solicitud de vacaciones creada: {RequestId} para Employee {EmployeeId}, CorrelationId: {CorrelationId}",
                createdRequest.Id,
                employeeId,
                correlationId);

            // PRG pattern: redirigir tras creación exitosa al dashboard del empleado (Razor Page)
            TempData["SuccessMessage"] = "Solicitud creada exitosamente.";
            return RedirectToPage("/Employee/Dashboard");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error de negocio al crear solicitud para Employee {EmployeeId}.", employeeId);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (FluentValidation.ValidationException vex)
        {
            foreach (var error in vex.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return View(model);
        }
    }

    // POST: /EmployeeRequests/Cancel/{id}
    // T311: Cancels a pending vacation request (US3, Phase 5)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            _logger.LogWarning("Cancel request POST sin EmployeeId en claims.");
            return Unauthorized();
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            _logger.LogError("Empleado {EmployeeId} no encontrado al cancelar solicitud.", employeeId);
            return NotFound("Empleado no encontrado.");
        }

        var command = new CancelRequestCommand(id, Guid.NewGuid().ToString());

        try
        {
            await _cancelHandler.Handle(command, employee, HttpContext.RequestAborted);
            TempData["SuccessMessage"] = "Solicitud cancelada exitosamente.";
            return RedirectToPage("/Employee/Dashboard");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al cancelar solicitud {RequestId}.", id);
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToPage("/Employee/Dashboard");
        }
    }

    // POST: /EmployeeRequests/Void/{id}
    // T411 [P] [US4] Void POST action (owner policy, confirmation, SR-005 antiforgery)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(Guid id, string reason)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            _logger.LogWarning("Void request POST sin EmployeeId en claims.");
            return Unauthorized();
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            _logger.LogError("Empleado {EmployeeId} no encontrado al anular solicitud.", employeeId);
            return NotFound("Empleado no encontrado.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "El motivo de anulación es obligatorio.";
            return RedirectToPage("/Employee/Dashboard");
        }

        var command = new VoidRequestCommand(id, reason, Guid.NewGuid().ToString());

        try
        {
            await _voidHandler.Handle(command, employee, HttpContext.RequestAborted);
            TempData["SuccessMessage"] = "Solicitud anulada exitosamente.";
            return RedirectToPage("/Employee/Dashboard");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al anular solicitud {RequestId}.", id);
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToPage("/Employee/Dashboard");
        }
    }

    private Guid GetEmployeeIdFromClaims()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
