using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Application.Features.Authentication.Contracts;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Application.Features.VacationRequest.Create;
using NovaLeave.Infrastructure.Configuration;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Infrastructure.Persistence;
using NovaLeave.Infrastructure.Services;
using NovaLeave.Presentation.Web.Authorization;
using NovaLeave.Presentation.Web.Filters;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog structured logging (spec_001 T002, architecture.md Observability)
// Read configuration + enrich with correlation-id / request-id in the middleware below.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {RequestId} {Message:lj}{NewLine}{Exception}");
});

// Add services to the container.

// Configure DbContext with SQL Server (or InMemory for testing)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (builder.Environment.EnvironmentName == "Testing" || string.IsNullOrEmpty(connectionString))
{
    // Use InMemory database for testing
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("NovaLeaveTestDb"));
}
else
{
    // Use SQL Server for production/development
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(
    IdentityConfiguration.ConfigureIdentityOptions)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    // Emite el claim "EmployeeId" en el principal (lo usan controllers, policies y dashboards)
    .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

// Configure authentication cookies
builder.Services.ConfigureApplicationCookie(
    CookieConfiguration.ConfigureCookieAuthenticationOptions);

// Configure security stamp validation interval (FR-010, SR-005)
// Revalidate security stamp every 5 minutes to detect role/claim changes mid-flight
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

// Register TimeProvider for testability (used by handlers that need time-dependent logic)
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// Register authentication services
builder.Services.AddScoped<IAuthenticationAuditService, AuthenticationAuditService>();
builder.Services.AddScoped<IAccountStatusValidator, AccountStatusValidator>();
builder.Services.AddScoped<IDefaultDashboardResolver, DefaultDashboardResolver>(); // CU-202 Role Switcher
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<LogoutCommandHandler>();

// Register employee services
builder.Services.AddScoped<IEmployeeSecurityService, EmployeeSecurityService>();

// Register development data seeder (only runs in Development environment)
builder.Services.AddScoped<DevelopmentDataSeeder>();

// Register TimeProvider for deterministic time in tests (Constitution §2.VI)
// Default to system time; tests replace with FakeTimeProvider
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// spec_001 T020-T022, T024, T025: vacation-request services + authorization policies
builder.Services.Configure<HolidayCalendarOptions>(
    builder.Configuration.GetSection(HolidayCalendarOptions.SectionName));
builder.Services.AddSingleton<IHolidayCalendar, HolidayCalendar>();
builder.Services.AddScoped<IWorkingDayCalculator, WorkingDayCalculator>();
builder.Services.AddScoped<IStateTransitionAuditService, StateTransitionAuditService>();

// T113, T117: VacationRequestRepository
builder.Services.AddScoped<IVacationRequestRepository, VacationRequestRepository>();

// T111, T112, T117: CreateRequestValidator + Handler
builder.Services.AddScoped<CreateRequestValidator>();
builder.Services.AddScoped<IValidator<CreateRequestCommand>, CreateRequestValidator>();
builder.Services.AddScoped<CreateRequestHandler>();

// T109: ListPendingForApproverHandler
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.List.ListPendingForApproverHandler>();

// T510: ListMyRequestsHandler (Phase 7, US5)
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.List.ListMyRequestsHandler>();

// T210, T211: Approve/Reject handlers (Phase 4, US2)
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.Approve.ApproveRequestHandler>();
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.Reject.RejectRequestValidator>();
builder.Services.AddScoped<IValidator<NovaLeave.Application.Features.VacationRequest.Reject.RejectRequestCommand>, NovaLeave.Application.Features.VacationRequest.Reject.RejectRequestValidator>();
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.Reject.RejectRequestHandler>();

// T310: Cancel handler (Phase 5, US3)
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.Cancel.CancelRequestHandler>();

// T410: Void handler (Phase 6, US4)
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.Void.VoidRequestHandler>();

// T602: Edit handler (Phase 8, FR-009)
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.Edit.EditRequestValidator>();
builder.Services.AddScoped<IValidator<NovaLeave.Application.Features.VacationRequest.Edit.EditRequestCommand>, NovaLeave.Application.Features.VacationRequest.Edit.EditRequestValidator>();
builder.Services.AddScoped<NovaLeave.Application.Features.VacationRequest.Edit.EditRequestHandler>();

// Authorization handlers y políticas (SR-007, FR-012)
builder.Services.AddScoped<IAuthorizationHandler, OwnsRequestHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IsAssignedApproverHandler>();
builder.Services.AddScoped<IAuthorizationHandler, NotSelfApprovalHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(RequestPolicies.OwnsRequest, p => p.Requirements.Add(new OwnsRequestRequirement()));
    options.AddPolicy(RequestPolicies.IsAssignedApprover, p => p.Requirements.Add(new IsAssignedApproverRequirement()));
    options.AddPolicy(RequestPolicies.NotSelfApproval, p => p.Requirements.Add(new NotSelfApprovalRequirement()));
});

// Register filters
builder.Services.AddScoped<ValidateAccountStatusFilter>();

// Configure rate limiting (FR-017) - disabled in Testing environment
if (builder.Environment.EnvironmentName != "Testing")
{
    builder.Services.AddRateLimiter(RateLimitingConfiguration.ConfigureRateLimiting);
}

builder.Services.AddRazorPages();

// Register global antiforgery filter for MVC controllers (SR-004)
// Register ValidateAccountStatusFilter to check account status on protected actions (FR-011)
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // No agregar ValidateAccountStatusFilter globalmente porque causaría problemas en Account/Login
    // Se debe aplicar selectivamente en controladores/acciones que requieran cuenta activa
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Serve static files BEFORE other middleware (CSS, JS, images from wwwroot)
// This must come early in the pipeline to avoid authentication/authorization checks for static assets
app.UseStaticFiles();

app.UseHttpsRedirection();

// Correlation-id middleware: reads/creates X-Correlation-ID and pushes it (plus request id)
// into Serilog LogContext so every log line during the request is enriched (spec_001 T002).
app.Use(async (context, next) =>
{
    const string correlationHeader = "X-Correlation-ID";
    var correlationId = context.Request.Headers.TryGetValue(correlationHeader, out var incoming)
        && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("N");

    context.Response.Headers[correlationHeader] = correlationId;
    context.Items["CorrelationId"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

app.UseRouting();

// Re-execute unmatched routes (HTTP 404) to /Home/Error (spec_003 §3.9, SR-003).
// Restricted to 404 so anti-forgery 400s and authorization 403s keep their status codes.
app.UseStatusCodePages(async statusCodeContext =>
{
    if (statusCodeContext.HttpContext.Response.StatusCode != StatusCodes.Status404NotFound)
    {
        return;
    }

    var originalPath = statusCodeContext.HttpContext.Request.Path;
    var originalQueryString = statusCodeContext.HttpContext.Request.QueryString;

    statusCodeContext.HttpContext.Request.Path = "/Home/Error";
    statusCodeContext.HttpContext.Request.QueryString = QueryString.Empty;
    try
    {
        await statusCodeContext.Next(statusCodeContext.HttpContext);
    }
    finally
    {
        statusCodeContext.HttpContext.Request.Path = originalPath;
        statusCodeContext.HttpContext.Request.QueryString = originalQueryString;
    }
});

// Enable rate limiting middleware (FR-017) - disabled in Testing environment
if (!app.Environment.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase))
{
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed development data on startup (only in Development environment)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
    await seeder.SeedAsync();
}

app.Run();
