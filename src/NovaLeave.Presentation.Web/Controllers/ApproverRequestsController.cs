using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Application.Features.VacationRequest.Approve;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Application.Features.VacationRequest.List;
using NovaLeave.Application.Features.VacationRequest.Reject;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Presentation.Web.ViewModels;

namespace NovaLeave.Presentation.Web.Controllers;

// T214: Controller for approver requests (US2, Phase 4).
// Handles listing pending requests, viewing details, and approve/reject actions.
[Authorize]
[Route("approver/requests")]
public class ApproverRequestsController : Controller
{
    private readonly ListPendingForApproverHandler _listHandler;
    private readonly ApproveRequestHandler _approveHandler;
    private readonly RejectRequestHandler _rejectHandler;
    private readonly ApplicationDbContext _context;
    private readonly IVacationRequestRepository _requestRepository;

    public ApproverRequestsController(
        ListPendingForApproverHandler listHandler,
        ApproveRequestHandler approveHandler,
        RejectRequestHandler rejectHandler,
        ApplicationDbContext context,
        IVacationRequestRepository requestRepository)
    {
        _listHandler = listHandler ?? throw new ArgumentNullException(nameof(listHandler));
        _approveHandler = approveHandler ?? throw new ArgumentNullException(nameof(approveHandler));
        _rejectHandler = rejectHandler ?? throw new ArgumentNullException(nameof(rejectHandler));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _requestRepository = requestRepository ?? throw new ArgumentNullException(nameof(requestRepository));
    }

    private Guid GetEmployeeIdFromClaims()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    // GET: /approver/requests
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new ListPendingForApproverQuery(employeeId, page, 20);
        var requests = await _listHandler.Handle(query, cancellationToken);

        return View(requests);
    }

    // GET: /approver/requests/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken = default)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            return Unauthorized();
        }

        var request = await _requestRepository.GetByIdAsync(id, cancellationToken);
        if (request == null)
        {
            return NotFound();
        }

        // Verificar que el empleado es el aprobador asignado
        if (request.Owner.AssignedApproverId != employeeId)
        {
            return NotFound(); // Existence hiding
        }

        var viewModel = new RequestDetailViewModel
        {
            Id = request.Id,
            EmployeeName = request.Owner.Name,
            EmployeeEmail = request.Owner.Email,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RequestedDays = request.RequestedDays,
            Reason = request.Reason,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            ExpiryDate = request.ExpiryDate,
            EmployeeCurrentBalance = request.Owner.Balance,
            ResolvedAt = request.ResolvedAt,
            RejectionReason = request.RejectionReason
        };

        // Resolver nombre del aprobador si la solicitud ya fue resuelta
        if (request.ResolvedBy.HasValue)
        {
            var resolver = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == request.ResolvedBy.Value, cancellationToken);
            viewModel.ResolvedByFullName = resolver?.Name;
        }

        return View(viewModel);
    }

    // POST: /approver/requests/{id}/approve
    [HttpPost("{id:guid}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken = default)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            return Unauthorized();
        }

        var request = await _requestRepository.GetByIdAsync(id, cancellationToken);
        if (request == null)
        {
            return NotFound();
        }

        // Verificar que el empleado es el aprobador asignado
        if (request.Owner.AssignedApproverId != employeeId)
        {
            return NotFound(); // Existence hiding
        }

        var command = new ApproveRequestCommand(id, employeeId, Guid.NewGuid().ToString());

        var requestOwner = await _context.Employees.FindAsync(new object[] { request.OwnerId }, cancellationToken);
        if (requestOwner == null)
        {
            return NotFound("Empleado propietario de la solicitud no encontrado.");
        }

        await _approveHandler.Handle(command, requestOwner, cancellationToken);

        TempData["SuccessMessage"] = "Solicitud aprobada exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /approver/requests/{id}/reject
    [HttpPost("{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, [FromForm] RejectRequestViewModel viewModel, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            return Unauthorized();
        }

        var request = await _requestRepository.GetByIdAsync(id, cancellationToken);
        if (request == null)
        {
            return NotFound();
        }

        // Verificar que el empleado es el aprobador asignado
        if (request.Owner.AssignedApproverId != employeeId)
        {
            return NotFound(); // Existence hiding
        }

        var command = new RejectRequestCommand(
            id,
            employeeId,
            viewModel.RejectionReason,
            Guid.NewGuid().ToString());

        var requestOwner = await _context.Employees.FindAsync(new object[] { request.OwnerId }, cancellationToken);
        if (requestOwner == null)
        {
            return NotFound("Empleado propietario de la solicitud no encontrado.");
        }

        await _rejectHandler.Handle(command, requestOwner, cancellationToken);

        TempData["SuccessMessage"] = "Solicitud rechazada exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /approver/requests/history
    // Muestra solicitudes resueltas (Approved/Rejected) asignadas a este aprobador.
    [HttpGet("history")]
    public async Task<IActionResult> History(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            return Unauthorized();
        }

        var offset = (page - 1) * pageSize;

        var query = _context.VacationRequests
            .Include(r => r.Owner)
            .Where(r => r.Owner.AssignedApproverId == employeeId)
            .Where(r => r.Status == Domain.Entities.RequestStatus.Approved ||
                        r.Status == Domain.Entities.RequestStatus.Rejected)
            .OrderByDescending(r => r.ResolvedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var requests = await query.Skip(offset).Take(pageSize).ToListAsync(cancellationToken);

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(requests);
    }
}
