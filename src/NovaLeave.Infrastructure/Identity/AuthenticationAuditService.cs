using NovaLeave.Application.Features.Authentication.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Identity;

// Implementation of IAuthenticationAuditService
// Persists authentication events to AuditRecords table (FR-018)
// DOES NOT log passwords, tokens, or sensitive PII (FR-019)
public class AuthenticationAuditService : IAuthenticationAuditService
{
    private readonly ApplicationDbContext _context;

    public AuthenticationAuditService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task LogLoginSuccessAsync(
        string userId,
        string email,
        string role,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        var auditRecord = new AuditRecord(
            action: "LoginSuccess",
            result: "Success",
            correlationId: correlationId,
            actorId: userId,
            actorRole: role,
            entityType: "Session",
            entityId: null,
            reason: null,
            requestId: null,
            details: $"Email: {email}");

        _context.AuditRecords.Add(auditRecord);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LogLoginFailureAsync(
        string email,
        string? userId = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        // Note: We log the attempted email (not sensitive) but NOT the password
        var auditRecord = new AuditRecord(
            action: "LoginFailure",
            result: "Failure",
            correlationId: correlationId,
            actorId: userId ?? email,
            actorRole: null,
            entityType: "Session",
            entityId: null,
            reason: reason,
            requestId: null,
            details: $"Email: {email}");

        _context.AuditRecords.Add(auditRecord);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LogLogoutAsync(
        string userId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        var auditRecord = new AuditRecord(
            action: "Logout",
            result: "Success",
            correlationId: correlationId,
            actorId: userId,
            actorRole: null,
            entityType: "Session",
            entityId: null,
            reason: null,
            requestId: null,
            details: $"Email: {email}");

        _context.AuditRecords.Add(auditRecord);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LogAccountLockedAsync(
        string userId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        var auditRecord = new AuditRecord(
            action: "AccountLocked",
            result: "Success",
            correlationId: correlationId,
            actorId: userId,
            actorRole: null,
            entityType: "Account",
            entityId: userId,
            reason: "MaxFailedAccessAttempts",
            requestId: null,
            details: $"Email: {email}");

        _context.AuditRecords.Add(auditRecord);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
