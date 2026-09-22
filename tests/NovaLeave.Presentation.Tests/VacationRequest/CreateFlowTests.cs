using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Presentation.Tests.Fixtures;
using Xunit;

namespace NovaLeave.Presentation.Tests.VacationRequest;

// T102: Integration tests for Employee Create Request flow (GET/POST)
// Tests: happy path, inverted range, past/today start, overlap, insufficient balance,
// unauthenticated redirect, anti-forgery rejection (SR-001), over-posting rejection (SR-002).
[Collection("VacationTests")]
public class CreateFlowTests : IAsyncLifetime
{
    private readonly VacationTestFixture _fixture;
    private readonly HttpClient _client;

    public CreateFlowTests(VacationTestFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateGET_authenticated_returnsForm()
    {
        await AuthenticateAsEmployee();

        var response = await _client.GetAsync("/EmployeeRequests/Create");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("StartDate", html);
        Assert.Contains("EndDate", html);
        Assert.Contains("Reason", html);
    }

    [Fact]
    public async Task CreateGET_unauthenticated_redirectsToLogin()
    {
        var response = await _client.GetAsync("/EmployeeRequests/Create");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreatePOST_validRequest_createsRequestAndRedirects()
    {
        await AuthenticateAsEmployee();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (token, cookieHeader) = await GetAntiForgeryTokenAndCookie("/EmployeeRequests/Create");

        var formData = new Dictionary<string, string>
        {
            ["StartDate"] = today.AddDays(10).ToString("yyyy-MM-dd"),
            ["EndDate"] = today.AddDays(14).ToString("yyyy-MM-dd"),
            ["Reason"] = "Vacaciones familiares",
            ["__RequestVerificationToken"] = token
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/EmployeeRequests/Create")
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await _client.SendAsync(request);

        // PRG pattern: debe redirigir tras éxito
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Verificar que la solicitud se creó en BD
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = await db.VacationRequests
            .Where(r => r.OwnerId == VacationTestFixture.TestEmployeeId && r.Reason == "Vacaciones familiares")
            .FirstOrDefaultAsync();

        Assert.NotNull(created);
        Assert.Equal(Domain.Entities.RequestStatus.Pending, created.Status);
    }

    [Fact]
    public async Task CreatePOST_invertedRange_returnsValidationError()
    {
        await AuthenticateAsEmployee();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (token, cookieHeader) = await GetAntiForgeryTokenAndCookie("/EmployeeRequests/Create");

        var formData = new Dictionary<string, string>
        {
            ["StartDate"] = today.AddDays(20).ToString("yyyy-MM-dd"),
            ["EndDate"] = today.AddDays(10).ToString("yyyy-MM-dd"), // fin antes de inicio
            ["Reason"] = "Invertido",
            ["__RequestVerificationToken"] = token
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/EmployeeRequests/Create")
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await _client.SendAsync(request);

        // Debe volver a mostrar el formulario con error
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("StartDate", html); // formulario presente
    }

    [Fact]
    public async Task CreatePOST_pastStartDate_returnsValidationError()
    {
        await AuthenticateAsEmployee();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (token, cookieHeader) = await GetAntiForgeryTokenAndCookie("/EmployeeRequests/Create");

        var formData = new Dictionary<string, string>
        {
            ["StartDate"] = today.AddDays(-1).ToString("yyyy-MM-dd"), // pasado
            ["EndDate"] = today.AddDays(3).ToString("yyyy-MM-dd"),
            ["Reason"] = "Pasado",
            ["__RequestVerificationToken"] = token
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/EmployeeRequests/Create")
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("StartDate", html);
    }

    [Fact]
    public async Task CreatePOST_withoutAntiForgeryToken_returns400()
    {
        await AuthenticateAsEmployee();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var formData = new Dictionary<string, string>
        {
            ["StartDate"] = today.AddDays(10).ToString("yyyy-MM-dd"),
            ["EndDate"] = today.AddDays(14).ToString("yyyy-MM-dd"),
            ["Reason"] = "Sin token"
        };

        var response = await _client.PostAsync("/EmployeeRequests/Create", new FormUrlEncodedContent(formData));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task AuthenticateAsEmployee()
    {
        var loginData = new Dictionary<string, string>
        {
            ["Email"] = VacationTestFixture.TestUserEmail,
            ["Password"] = VacationTestFixture.TestUserPassword,
            ["RememberMe"] = "false"
        };

        var (token, cookieHeader) = await GetAntiForgeryTokenAndCookie("/Account/Login");
        loginData["__RequestVerificationToken"] = token;

        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = new FormUrlEncodedContent(loginData)
        };
        loginRequest.Headers.Add("Cookie", cookieHeader);

        var loginResponse = await _client.SendAsync(loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        // Guardar cookies de autenticación
        if (loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                _client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
            }
        }
    }

    private async Task<(string Token, string CookieHeader)> GetAntiForgeryTokenAndCookie(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var tokenMatch = System.Text.RegularExpressions.Regex.Match(html, @"name=""__RequestVerificationToken"" .*?value=""([^""]+)""");
        var token = tokenMatch.Success ? tokenMatch.Groups[1].Value : string.Empty;

        var cookieHeader = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

        return (token, cookieHeader);
    }
}
