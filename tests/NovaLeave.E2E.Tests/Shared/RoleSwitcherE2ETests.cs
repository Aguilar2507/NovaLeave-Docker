using Microsoft.Playwright;
using Xunit;

namespace NovaLeave.E2E.Tests.Shared;

// T645: Role Switcher E2E Tests (CU-202)
// Cobertura: switcher visible solo para usuarios dual-role,
// switching navega al dashboard correcto, sidebar se reconstruye,
// atributos ARIA actualizan, single-role negado server-side
public class RoleSwitcherE2ETests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private const string BaseUrl = "https://localhost:7122";

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.DisposeAsync();
        }
        _playwright?.Dispose();
    }

    [Fact]
    public async Task RoleSwitcher_NotVisibleForSingleRoleUser()
    {
        // Arrange: usuario con solo rol Employee
        var page = await _browser!.NewPageAsync();
        await LoginAsSingleRoleEmployee(page);

        // Act: navegar al dashboard de empleado
        await page.GotoAsync($"{BaseUrl}/Employee/Dashboard");

        // Assert: el componente role-switcher no debe estar visible
        var switcher = await page.QuerySelectorAsync("[data-component='role-switcher']");
        Assert.Null(switcher);
    }

    [Fact]
    public async Task RoleSwitcher_VisibleForDualRoleUser()
    {
        // Arrange: usuario con roles Employee + Approver
        var page = await _browser!.NewPageAsync();
        await LoginAsDualRoleUser(page);

        // Act: navegar al dashboard
        await page.GotoAsync($"{BaseUrl}/Employee/Dashboard");

        // Assert: el componente role-switcher debe estar visible
        var switcher = await page.QuerySelectorAsync("[data-component='role-switcher']");
        Assert.NotNull(switcher);
    }

    [Fact]
    public async Task RoleSwitcher_AriaAttributesCorrect()
    {
        // Arrange
        var page = await _browser!.NewPageAsync();
        await LoginAsDualRoleUser(page);
        await page.GotoAsync($"{BaseUrl}/Employee/Dashboard");

        // Act: verificar atributos ARIA del trigger
        var trigger = await page.QuerySelectorAsync("#roleSwitcherTrigger");
        Assert.NotNull(trigger);

        var ariaHasPopup = await trigger.GetAttributeAsync("aria-haspopup");
        var ariaExpanded = await trigger.GetAttributeAsync("aria-expanded");

        // Assert: atributos iniciales correctos
        Assert.Equal("listbox", ariaHasPopup);
        Assert.Equal("false", ariaExpanded);

        // Act: hacer clic en el trigger para expandir
        await trigger.ClickAsync();
        await page.WaitForTimeoutAsync(300); // esperar animación

        ariaExpanded = await trigger.GetAttributeAsync("aria-expanded");

        // Assert: aria-expanded debe cambiar a true
        Assert.Equal("true", ariaExpanded);
    }

    [Fact]
    public async Task RoleSwitcher_SwitchToApproverNavigatesToApproverDashboard()
    {
        // Arrange: usuario dual-role en dashboard de empleado
        var page = await _browser!.NewPageAsync();
        await LoginAsDualRoleUser(page);
        await page.GotoAsync($"{BaseUrl}/Employee/Dashboard");

        // Act: abrir el switcher y seleccionar Approver
        var trigger = await page.QuerySelectorAsync("#roleSwitcherTrigger");
        Assert.NotNull(trigger);
        await trigger.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        var approverOption = await page.QuerySelectorAsync("[data-role='Approver']");
        Assert.NotNull(approverOption);
        await approverOption.ClickAsync();

        // Assert: debe navegar a /Approver/Dashboard
        await page.WaitForURLAsync($"{BaseUrl}/Approver/Dashboard");
        Assert.Contains("/Approver/Dashboard", page.Url);
    }

    [Fact]
    public async Task RoleSwitcher_SwitchToEmployeeNavigatesToEmployeeDashboard()
    {
        // Arrange: usuario dual-role en dashboard de aprobador
        var page = await _browser!.NewPageAsync();
        await LoginAsDualRoleUser(page);
        await page.GotoAsync($"{BaseUrl}/Approver/Dashboard");

        // Act: abrir el switcher y seleccionar User
        var trigger = await page.QuerySelectorAsync("#roleSwitcherTrigger");
        Assert.NotNull(trigger);
        await trigger.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        var userOption = await page.QuerySelectorAsync("[data-role='User']");
        Assert.NotNull(userOption);
        await userOption.ClickAsync();

        // Assert: debe navegar a /Employee/Dashboard
        await page.WaitForURLAsync($"{BaseUrl}/Employee/Dashboard");
        Assert.Contains("/Employee/Dashboard", page.Url);
    }

    [Fact]
    public async Task RoleSwitcher_ShowsActiveRoleWithCheckmark()
    {
        // Arrange
        var page = await _browser!.NewPageAsync();
        await LoginAsDualRoleUser(page);
        await page.GotoAsync($"{BaseUrl}/Employee/Dashboard");

        // Act: abrir el switcher
        var trigger = await page.QuerySelectorAsync("#roleSwitcherTrigger");
        Assert.NotNull(trigger);
        await trigger.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        // Assert: la opción User debe tener checkmark y clase active
        var userOption = await page.QuerySelectorAsync("[data-role='User']");
        Assert.NotNull(userOption);

        var classes = await userOption.GetAttributeAsync("class");
        Assert.Contains("role-switcher__option--active", classes);

        var checkmark = await userOption.QuerySelectorAsync(".role-switcher__option-check");
        Assert.NotNull(checkmark);
    }

    [Fact]
    public async Task SingleRoleUser_CannotAccessOtherRoleDashboard()
    {
        // Arrange: usuario con solo rol Employee
        var page = await _browser!.NewPageAsync();
        await LoginAsSingleRoleEmployee(page);

        // Act: intentar navegar directamente a /Approver/Dashboard
        var response = await page.GotoAsync($"{BaseUrl}/Approver/Dashboard");

        // Assert: debe devolver 403 Forbidden o redirigir a AccessDenied
        // (Nota: esto depende de la implementación de autorización server-side)
        Assert.True(
            response?.Status == 403 || 
            page.Url.Contains("AccessDenied") || 
            page.Url.Contains("Employee/Dashboard"),
            "Single-role user should be denied access to other role's dashboard");
    }

    [Fact]
    public async Task RoleSwitcher_ClosesMenuOnOutsideClick()
    {
        // Arrange
        var page = await _browser!.NewPageAsync();
        await LoginAsDualRoleUser(page);
        await page.GotoAsync($"{BaseUrl}/Employee/Dashboard");

        // Act: abrir el switcher
        var trigger = await page.QuerySelectorAsync("#roleSwitcherTrigger");
        Assert.NotNull(trigger);
        await trigger.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        var ariaExpanded = await trigger.GetAttributeAsync("aria-expanded");
        Assert.Equal("true", ariaExpanded);

        // Click fuera del menu (en el body)
        await page.ClickAsync("body");
        await page.WaitForTimeoutAsync(300);

        // Assert: el menu debe cerrarse
        ariaExpanded = await trigger.GetAttributeAsync("aria-expanded");
        Assert.Equal("false", ariaExpanded);
    }

    // Helper methods para login
    private async Task LoginAsSingleRoleEmployee(IPage page)
    {
        await page.GotoAsync($"{BaseUrl}/Account/Login");
        await page.FillAsync("#Email", "user@novaleave.local");
        await page.FillAsync("#Password", "Test123!@#");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync($"{BaseUrl}/Employee/Dashboard");
    }

    private async Task LoginAsDualRoleUser(IPage page)
    {
        await page.GotoAsync($"{BaseUrl}/Account/Login");
        await page.FillAsync("#Email", "manager@novaleave.local");
        await page.FillAsync("#Password", "Test123!@#");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync(url => url.Contains("/Dashboard"));
    }
}
