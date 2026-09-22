namespace NovaLeave.Application.Features.Authentication.Logout;

// Command to request user logout
// Empty record for consistency with CQRS pattern
// All necessary context (user identity) is retrieved from HttpContext by the handler
public sealed record LogoutCommand;
