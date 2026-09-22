# NovaLeave Copilot Instructions

## Project Context
- .NET 10, C#
- Razor Pages exists; prioritize Razor Pages over Blazor or MVC unless the spec explicitly requires controllers
- Clean Architecture: Domain → Application → Infrastructure → Presentation.Web
- SQL Server, EF Core, ASP.NET Core Identity, Serilog, FluentValidation

## Code Style
- Use inline comments (`//`) to explain intent, risk, or rationale
- Do not use XML documentation comments unless explicitly required by a framework or tool
- Keep comments concise and meaningful; do not repeat the code
- User-facing messages and UI text should be in Spanish unless a spec says otherwise
- Prefer clear, maintainable code over clever code

## Architecture Rules
- Domain contains business rules and invariants
- Application orchestrates use cases
- Infrastructure implements contracts and data access
- Presentation.Web should stay thin and only coordinate UI flow
- Do not expose EF entities directly to Razor views; use ViewModels
- Keep balance logic inside the `Employee` entity as the single source of truth
- Use `TimeProvider` for all time-dependent behavior
- Keep all I/O async/await end-to-end

## Testing Rules
- Follow test-first development: Red → Green → Refactor
- Use xUnit for unit and integration tests
- Use `WebApplicationFactory<Program>` for integration tests
- Use Playwright for E2E tests
- Use deterministic fake time in tests; avoid `DateTime.Now` and `Thread.Sleep`
- Critical Domain and Application modules should target at least 80% line coverage

## Security Rules
- Validate anti-forgery tokens on all state-changing requests
- Use Post/Redirect/Get for successful form submissions
- Enforce existence hiding with 404 for unauthorized resource access
- Use dedicated input models to prevent over-posting
- Revalidate identity and authorization at execution time, not only when the page loads

## Key References
- `.specify/memory/constitution.md`
- `.specify/specs/common/architecture.md`
- `.specify/specs/common/data-model.md`
- `.specify/specs/common/security.md`
- `.specify/specs/001-vacation-request/tasks.md`
- `.specify/specs/004-initial-setup-and-authentication/tasks.md`
