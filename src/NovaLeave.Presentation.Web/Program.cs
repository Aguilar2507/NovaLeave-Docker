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
using NovaLeave.Infrastructure.Observability;
using NovaLeave.Infrastructure.Persistence;
using NovaLeave.Infrastructure.Services;
using NovaLeave.Presentation.Web.Authorization;
using NovaLeave.Presentation.Web.Filters;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// spec_007 FR-018: the port /metrics is bound to. Kestrel must be listening on it
// (ASPNETCORE_HTTP_PORTS in compose.yaml), and compose must NOT publish it to the host.
// Overridable so the port can be changed without a rebuild.
var metricsPort = builder.Configuration.GetValue<int?>("Metrics:Port") ?? 9464;

// Configure Serilog structured logging (spec_001 T002, architecture.md Observability)
// Read configuration + enrich with correlation-id / request-id in the middleware below.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    // spec_009: console format is configurable because the two consumers want opposite things.
    // A human reading `docker compose logs` wants an aligned line; Loki wants JSON so that
    // CorrelationId, RequestId and the message template survive as queryable FIELDS instead of
    // being flattened into one opaque string.
    //
    // compose.yaml sets this to "json" for the container. The default stays "text" so that
    // running outside Docker keeps the readable output this project has always had.
    var consoleFormat = context.Configuration.GetValue<string>("Serilog:ConsoleFormat") ?? "text";

    if (string.Equals(consoleFormat, "json", StringComparison.OrdinalIgnoreCase))
    {
        // CLEF (Compact Log Event Format): one JSON object per line, which is exactly what a
        // log shipper can parse without guessing at a text layout.
        configuration.WriteTo.Console(new CompactJsonFormatter());
    }
    else
    {
        configuration.WriteTo.Console(
            outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {RequestId} {Message:lj}{NewLine}{Exception}");
    }
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
// spec_007 T204: the concrete service is registered by type, and the interface resolves to the
// metrics decorator wrapping it. Consumers keep depending on IAuthenticationAuditService and are
// unaware of the decoration (FR-010).
builder.Services.AddScoped<AuthenticationAuditService>();
builder.Services.AddScoped<IAuthenticationAuditService>(sp =>
    new MetricsAuthenticationAuditService(
        sp.GetRequiredService<AuthenticationAuditService>(),
        sp.GetRequiredService<NovaLeaveMetrics>()));
builder.Services.AddScoped<IAccountStatusValidator, AccountStatusValidator>();
builder.Services.AddScoped<IDefaultDashboardResolver, DefaultDashboardResolver>(); // CU-202 Role Switcher
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<LogoutCommandHandler>();

// Register employee services
builder.Services.AddScoped<IEmployeeSecurityService, EmployeeSecurityService>();

// Register development data seeder (only runs in Development environment)
builder.Services.AddScoped<DevelopmentDataSeeder>();
builder.Services.AddScoped<IDevelopmentDataSeeder>(sp => sp.GetRequiredService<DevelopmentDataSeeder>());

// spec_005 T102/T105: startup database initialization (migrate + seed), guarded against Production.
// Replaces the manual "apply migrations by hand" step that made a fresh container unusable.
builder.Services.Configure<DatabaseInitializerOptions>(
    builder.Configuration.GetSection(DatabaseInitializerOptions.SectionName));
builder.Services.AddScoped<IDatabaseMigrator, EfCoreDatabaseMigrator>();
builder.Services.AddScoped<DatabaseInitializer>();

// spec_005 T103/FR-018: health endpoint including database reachability, so container health
// reflects whether the app can actually serve requests rather than merely that the process runs.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

// spec_007 T102/T103: OpenTelemetry metrics exported in Prometheus format.
// Delivers the "Metrics" half of the Observability baseline in common/architecture.md, which
// no previous feature had implemented -- until now §12.1's latency targets were unmeasurable.
builder.Services.AddSingleton<NovaLeaveMetrics>();
builder.Services.AddOpenTelemetry()
    // Without this the service reports as "unknown_service:NovaLeave.Presentation.Web".
    // Prometheus and Grafana key dashboards off the service name, so setting it now avoids
    // rewriting queries later.
    .ConfigureResource(resource => resource.AddService(
        serviceName: "novaleave-web",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"))
    .WithMetrics(metrics =>
    {
        metrics
            // Built-in ASP.NET Core meters, subscribed by name. ASP.NET Core 8+ emits these
            // natively through System.Diagnostics.Metrics, so no instrumentation package is
            // needed -- OpenTelemetry.Instrumentation.AspNetCore now covers tracing only, and
            // its metrics extension no longer exists.
            //
            // These use ROUTE TEMPLATES, not raw paths, which is what keeps their cardinality
            // bounded. Do not "improve" this into raw paths (research.md R-005).
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddMeter("Microsoft.AspNetCore.Routing")
            .AddMeter("Microsoft.AspNetCore.Diagnostics")
            // Rate limiting is required by Constitution §7.2 but has never been verified
            // (docs/Keep-in-mind.md item 14): its tests are disabled in the Testing
            // environment. These metrics make the limiter observable for the first time.
            .AddMeter("Microsoft.AspNetCore.RateLimiting")
            // .NET runtime: GC, thread pool, memory, exception counts. Native since .NET 9,
            // so like the ASP.NET Core meters this needs no instrumentation package.
            .AddMeter("System.Runtime")
            // EF Core's native meter, subscribed by name rather than through the beta
            // instrumentation package (research.md R-001).
            .AddMeter("Microsoft.EntityFrameworkCore")
            .AddMeter(NovaLeaveMetrics.MeterName)
            .AddPrometheusExporter();
    });

// Register TimeProvider for deterministic time in tests (Constitution §2.VI)
// Default to system time; tests replace with FakeTimeProvider
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// spec_001 T020-T022, T024, T025: vacation-request services + authorization policies
builder.Services.Configure<HolidayCalendarOptions>(
    builder.Configuration.GetSection(HolidayCalendarOptions.SectionName));
builder.Services.AddSingleton<IHolidayCalendar, HolidayCalendar>();
builder.Services.AddScoped<IWorkingDayCalculator, WorkingDayCalculator>();
// spec_007 T204: decorated with metrics. Every one of the six vacation-request handlers calls
// through this contract, so wrapping it counts every state transition without touching them.
builder.Services.AddScoped<StateTransitionAuditService>();
builder.Services.AddScoped<IStateTransitionAuditService>(sp =>
    new MetricsStateTransitionAuditService(
        sp.GetRequiredService<StateTransitionAuditService>(),
        sp.GetRequiredService<NovaLeaveMetrics>()));

// spec_007 T203: queue-state gauges. A BackgroundService so the host starts it automatically --
// an ObservableGauge only reports while the object that registered it is alive.
builder.Services.AddHostedService<VacationRequestMetricsCollector>();

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

// spec_005 T103: mapped before authentication so container and orchestrator probes need no
// credentials. Exposes status only -- no diagnostics detail, which Constitution §7.2 prohibits
// from being publicly reachable.
app.MapHealthChecks("/health");

// spec_007 T104/T401/FR-018: Prometheus scraping endpoint, served ONLY on the metrics port.
// Mapped before authentication so a scrape needs no credentials or cookie (US4-3).
//
// RequireHost pins this endpoint to port 9464, which compose.yaml deliberately does not publish
// to the host. The application port (8080) is published, and on that port /metrics returns 404.
//
// This is not belt-and-braces: Phase 4's negative test proved that serving /metrics on the
// application port exposed it to anyone who could reach the app, because publishing a port
// publishes every path on it. Binding metrics to a separate, unpublished port is what actually
// enforces Constitution §7.2's prohibition on publicly exposed diagnostics
// (research.md R-006 amendment).
app.MapPrometheusScrapingEndpoint().RequireHost($"*:{metricsPort}");

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

// spec_005 T105: apply pending migrations and seed development data before serving traffic.
// The Production guard and the retry budget live in DatabaseInitializer, not here, so that the
// guard is unit-testable rather than an untested condition in the composition root.
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Run();
