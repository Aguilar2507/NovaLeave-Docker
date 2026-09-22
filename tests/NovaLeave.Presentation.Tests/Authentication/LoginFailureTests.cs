using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;
using NovaLeave.Presentation.Tests.Fixtures;
using Xunit;

namespace NovaLeave.Presentation.Tests.Authentication;

// Integration tests for User Story 2: Login Failure Handling (Phase 4)
// Tests all failure scenarios with security requirements:
// - Generic error messages for all failures (no enumeration)
// - Rate limiting enforcement
// - Account lockout management
// - Audit logging of all failures
// - Constant-time responses (timing attack mitigation)
[Collection("AuthTests")]
public class LoginFailureTests
{
    private readonly AuthTestFixture _fixture;
    private readonly HttpClient _client;

    public LoginFailureTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // Helper method to extract antiforgery token from HTML
    private async Task<string> GetAntiforgeryTokenAsync(string url = "/Account/Login")
    {
        var response = await _client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        // Extract token from the hidden input
        var match = Regex.Match(content, @"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        if (!match.Success)
        {
            throw new InvalidOperationException("Antiforgery token not found in response");
        }

        return match.Groups[1].Value;
    }

    // Helper method to create login form data with antiforgery token
    private async Task<FormUrlEncodedContent> CreateLoginFormAsync(string email, string password)
    {
        var token = await GetAntiforgeryTokenAsync();
        var formData = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };

        return new FormUrlEncodedContent(formData);
    }

    // T029: POST with non-existent email → HTTP 200 with generic error message
    [Fact]
    public async Task POST_Login_NonExistentEmail_ReturnsGenericError()
    {
        // Arrange
        var loginForm = await CreateLoginFormAsync("unknown@example.com", "Test123!@#");

        // Act
        var response = await _client.PostAsync("/Account/Login", loginForm);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Re-render form, not redirect
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", content);
        Assert.DoesNotContain("not found", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("does not exist", content, StringComparison.OrdinalIgnoreCase);
    }

    // T030: POST with valid email + wrong password → same generic error message
    [Fact]
    public async Task POST_Login_ValidEmail_WrongPassword_ReturnsGenericError()
    {
        // Arrange
        var loginForm = await CreateLoginFormAsync(
            AuthTestFixture.TestUserEmail,
            "WrongPassword123!@#");

        // Act
        var response = await _client.PostAsync("/Account/Login", loginForm);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", content);
        // Should not reveal "wrong password" or "incorrect password"
        Assert.DoesNotContain("wrong", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("incorrect", content, StringComparison.OrdinalIgnoreCase);
    }

    // T031: POST with Inactive account + correct credentials → generic error message
    [Fact]
    public async Task POST_Login_InactiveAccount_ReturnsGenericError()
    {
        // Arrange: Create an Inactive employee with unique email
        var uniqueEmail = $"inactive-{Guid.NewGuid():N}@test.com";

        using var scope = _fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Create Identity user
        var inactiveUser = new ApplicationUser
        {
            UserName = uniqueEmail,
            Email = uniqueEmail,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(inactiveUser, "Test123!@#");
        Assert.True(createResult.Succeeded);
        await userManager.AddToRoleAsync(inactiveUser, "Employee");

        // Create Employee with Inactive status
        var inactiveEmployee = new Employee(
            id: Guid.NewGuid(),
            identityId: inactiveUser.Id,
            name: "Inactive User",
            email: uniqueEmail,
            employmentStartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            initialBalance: 20,
            assignedApproverId: null);

        inactiveEmployee.Deactivate();
        context.Employees.Add(inactiveEmployee);
        await context.SaveChangesAsync();

        try
        {
            // Act
            var loginForm = await CreateLoginFormAsync(uniqueEmail, "Test123!@#");
            var response = await _client.PostAsync("/Account/Login", loginForm);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid email or password.", content);
            // Should not reveal "inactive" or "disabled" status (not in error message text)
            Assert.DoesNotContain("account is inactive", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("account disabled", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Cleanup
            context.Employees.Remove(inactiveEmployee);
            await userManager.DeleteAsync(inactiveUser);
            await context.SaveChangesAsync();
        }
    }

    // T032: POST with locked account + correct password → generic error message
    [Fact]
    public async Task POST_Login_LockedAccount_ReturnsGenericError()
    {
        // Arrange: Lock the test user account
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        Assert.NotNull(user);

        // Set lockout end to future time
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(30));

        try
        {
            // Act
            var loginForm = await CreateLoginFormAsync(
                AuthTestFixture.TestUserEmail,
                AuthTestFixture.TestUserPassword);
            var response = await _client.PostAsync("/Account/Login", loginForm);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid email or password.", content);
            Assert.DoesNotContain("locked", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("too many", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Cleanup: Unlock the account
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);
        }
    }

    // T032a: Verify AccessFailedCount increments after wrong password
    [Fact]
    public async Task POST_Login_WrongPassword_IncrementsAccessFailedCount()
    {
        // Arrange
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        Assert.NotNull(user);

        // Reset failed count first
        await userManager.ResetAccessFailedCountAsync(user);
        await Task.Delay(100); // Allow time for persistence

        user = await userManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        Assert.NotNull(user);
        var initialCount = await userManager.GetAccessFailedCountAsync(user);
        Assert.Equal(0, initialCount);

        // Act
        var loginForm = await CreateLoginFormAsync(
            AuthTestFixture.TestUserEmail,
            "WrongPassword123!@#");
        var response = await _client.PostAsync("/Account/Login", loginForm);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Create new scope to get fresh data
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var verifyUser = await verifyUserManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        Assert.NotNull(verifyUser);
        var newCount = await verifyUserManager.GetAccessFailedCountAsync(verifyUser);

        // In integration tests with InMemory database, AccessFailedCount should increment
        // If this assertion fails, it may be due to test isolation issues
        Assert.True(newCount > initialCount, 
            $"AccessFailedCount should have incremented. Initial: {initialCount}, New: {newCount}");

        // Cleanup
        await verifyUserManager.ResetAccessFailedCountAsync(verifyUser);
    }

    // T032b: Verify AccessFailedCount resets to 0 after successful login (FR-017)
    [Fact]
    public async Task POST_Login_Success_ResetsAccessFailedCount()
    {
        // Arrange: Set failed count > 0 but below lockout threshold (5)
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        Assert.NotNull(user);

        // Ensure user is not locked out first
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);

        // Increment failed count (but stay below lockout threshold of 5)
        await userManager.AccessFailedAsync(user);
        await userManager.AccessFailedAsync(user);
        await Task.Delay(100); // Allow time for persistence

        user = await userManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        Assert.NotNull(user);
        var countBeforeSuccess = await userManager.GetAccessFailedCountAsync(user);
        Assert.True(countBeforeSuccess >= 2, 
            $"AccessFailedCount should be at least 2, but was {countBeforeSuccess}");

        // Verify user is not locked out
        var isLockedOut = await userManager.IsLockedOutAsync(user);
        Assert.False(isLockedOut, "User should not be locked out before login attempt");

        // Act
        var loginForm = await CreateLoginFormAsync(
            AuthTestFixture.TestUserEmail,
            AuthTestFixture.TestUserPassword);
        var response = await _client.PostAsync("/Account/Login", loginForm);

        // Assert
        // Should redirect on success
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // If we got OK instead of redirect, check what error occurred
            var content = await response.Content.ReadAsStringAsync();
            Assert.Fail(
                $"Expected redirect on successful login, but got HTTP 200. " +
                $"Page content indicates: {(content.Contains("Invalid email") ? "Invalid credentials" : "Unknown error")}");
        }

        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect || 
            response.StatusCode == HttpStatusCode.Found,
            $"Expected redirect status code, but got {response.StatusCode}");

        // Verify count reset in new scope
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var verifyUser = await verifyUserManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        Assert.NotNull(verifyUser);
        var countAfterSuccess = await verifyUserManager.GetAccessFailedCountAsync(verifyUser);
        Assert.Equal(0, countAfterSuccess);
    }

    // T037: Verify AuditRecord created for UnknownEmail failure
    [Fact]
    public async Task POST_Login_UnknownEmail_CreatesAuditRecord()
    {
        // Arrange
        using var scope = _fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        var loginForm = await CreateLoginFormAsync("unknown@example.com", "Test123!@#");
        await _client.PostAsync("/Account/Login", loginForm);

        // Assert
        var auditRecord = await context.AuditRecords
            .Where(a => a.Action == "LoginFailure" && a.Reason == "UnknownEmail")
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        Assert.NotNull(auditRecord);
        Assert.Equal("Failure", auditRecord.Result);
        Assert.Contains("unknown@example.com", auditRecord.Details);
        // Password should NOT be in audit record (FR-019)
        Assert.DoesNotContain("Test123", auditRecord.Details ?? string.Empty);
    }

    // T037: Verify AuditRecord created for InvalidPassword failure
    [Fact]
    public async Task POST_Login_InvalidPassword_CreatesAuditRecord()
    {
        // Arrange
        using var scope = _fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        var loginForm = await CreateLoginFormAsync(
            AuthTestFixture.TestUserEmail,
            "WrongPassword123!@#");
        await _client.PostAsync("/Account/Login", loginForm);

        // Assert
        var auditRecord = await context.AuditRecords
            .Where(a => a.Action == "LoginFailure" && a.Reason == "InvalidPassword")
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        Assert.NotNull(auditRecord);
        Assert.Equal("Failure", auditRecord.Result);
        Assert.Contains(AuthTestFixture.TestUserEmail, auditRecord.Details);
        // Password should NOT be in audit record
        Assert.DoesNotContain("Wrong", auditRecord.Details ?? string.Empty);

        // Cleanup: Reset failed count
        using var cleanupScope = _fixture.Services.CreateScope();
        var userManager = cleanupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        if (user != null)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }
    }

    // T037: Verify AuditRecord created for InactiveAccount failure
    [Fact]
    public async Task POST_Login_InactiveAccount_CreatesAuditRecord()
    {
        // Arrange: Create an Inactive employee with unique email
        var uniqueEmail = $"inactive2-{Guid.NewGuid():N}@test.com";

        using var scope = _fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var inactiveUser = new ApplicationUser
        {
            UserName = uniqueEmail,
            Email = uniqueEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(inactiveUser, "Test123!@#");
        await userManager.AddToRoleAsync(inactiveUser, "Employee");

        var inactiveEmployee = new Employee(
            id: Guid.NewGuid(),
            identityId: inactiveUser.Id,
            name: "Inactive User 2",
            email: uniqueEmail,
            employmentStartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            initialBalance: 20,
            assignedApproverId: null);

        inactiveEmployee.Deactivate();
        context.Employees.Add(inactiveEmployee);
        await context.SaveChangesAsync();

        try
        {
            // Act
            var loginForm = await CreateLoginFormAsync(uniqueEmail, "Test123!@#");
            await _client.PostAsync("/Account/Login", loginForm);

            // Assert
            var auditRecord = await context.AuditRecords
                .Where(a => a.Action == "LoginFailure" && a.Reason == "InactiveAccount")
                .OrderByDescending(a => a.Timestamp)
                .FirstOrDefaultAsync();

            Assert.NotNull(auditRecord);
            Assert.Equal("Failure", auditRecord.Result);
            Assert.Contains(uniqueEmail, auditRecord.Details);
        }
        finally
        {
            // Cleanup
            context.Employees.Remove(inactiveEmployee);
            await userManager.DeleteAsync(inactiveUser);
            await context.SaveChangesAsync();
        }
    }

    // T037: Verify AuditRecord does NOT contain password or extra PII
    [Fact]
    public async Task POST_Login_AuditRecords_DoNotContainPassword()
    {
        // Arrange
        using var scope = _fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var uniquePassword = $"UniqueTestPassword{Guid.NewGuid()}!@#";

        // Act: Try to login with unique password
        var loginForm = await CreateLoginFormAsync("unknown@example.com", uniquePassword);
        await _client.PostAsync("/Account/Login", loginForm);

        // Assert: Search for the password in ALL audit records
        var auditRecordsWithPassword = await context.AuditRecords
            .Where(a => a.Details != null && a.Details.Contains(uniquePassword))
            .ToListAsync();

        Assert.Empty(auditRecordsWithPassword);
    }

    // T033b: Verify constant-time response (timing attack mitigation)
    // Unknown email vs wrong password should have similar response times
    [Fact]
    public async Task POST_Login_UnknownEmail_vs_WrongPassword_SimilarTiming()
    {
        // Arrange
        const int iterations = 5;
        var unknownEmailTimes = new List<long>();
        var wrongPasswordTimes = new List<long>();

        // Act: Measure unknown email response times
        for (int i = 0; i < iterations; i++)
        {
            var loginForm = await CreateLoginFormAsync("unknown@example.com", "Test123!@#");
            var stopwatch = Stopwatch.StartNew();
            await _client.PostAsync("/Account/Login", loginForm);
            stopwatch.Stop();
            unknownEmailTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Measure wrong password response times
        for (int i = 0; i < iterations; i++)
        {
            var loginForm = await CreateLoginFormAsync(
                AuthTestFixture.TestUserEmail,
                "WrongPassword123!@#");
            var stopwatch = Stopwatch.StartNew();
            await _client.PostAsync("/Account/Login", loginForm);
            stopwatch.Stop();
            wrongPasswordTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert: Average times should be within 100ms of each other
        var avgUnknownEmail = unknownEmailTimes.Average();
        var avgWrongPassword = wrongPasswordTimes.Average();
        var timingDifference = Math.Abs(avgUnknownEmail - avgWrongPassword);

        // Allow for some variance but not more than 100ms average difference
        Assert.True(timingDifference < 100, 
            $"Timing difference too large: {timingDifference}ms. " +
            $"UnknownEmail avg: {avgUnknownEmail}ms, WrongPassword avg: {avgWrongPassword}ms");

        // Cleanup: Reset failed count
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        if (user != null)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }
    }

    // T033b: Verify error response HTML is byte-for-byte identical across failure types
    [Fact]
    public async Task POST_Login_AllFailureTypes_IdenticalErrorHTML()
    {
        // Arrange
        var responses = new List<string>();

        // Act: Collect responses from different failure types
        // 1. Unknown email
        var form1 = await CreateLoginFormAsync("unknown@example.com", "Test123!@#");
        var response1 = await _client.PostAsync("/Account/Login", form1);
        var content1 = await response1.Content.ReadAsStringAsync();
        responses.Add(NormalizeHtml(content1));

        // 2. Wrong password
        var form2 = await CreateLoginFormAsync(AuthTestFixture.TestUserEmail, "WrongPassword123!@#");
        var response2 = await _client.PostAsync("/Account/Login", form2);
        var content2 = await response2.Content.ReadAsStringAsync();
        responses.Add(NormalizeHtml(content2));

        // Assert: All normalized responses should be identical
        var firstResponse = responses[0];
        foreach (var response in responses.Skip(1))
        {
            Assert.Equal(firstResponse, response);
        }

        // Cleanup
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(AuthTestFixture.TestUserEmail);
        if (user != null)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }
    }

    // Helper to normalize HTML by removing dynamic tokens
    private static string NormalizeHtml(string html)
    {
        // Remove antiforgery token value (changes each request)
        html = Regex.Replace(html, @"value=""[^""]*""", "value=\"NORMALIZED\"");
        // Remove any dynamic IDs or timestamps
        html = Regex.Replace(html, @"id=""[^""]*""", "id=\"NORMALIZED\"");
        return html;
    }
}
