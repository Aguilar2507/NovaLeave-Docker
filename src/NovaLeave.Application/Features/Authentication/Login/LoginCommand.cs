namespace NovaLeave.Application.Features.Authentication.Login;

/// <summary>
/// Command to authenticate a user with email and password.
/// Used by the Application layer to orchestrate the login flow.
/// Does not contain anti-forgery token (handled at Presentation layer).
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password,
    string? ReturnUrl = null
);