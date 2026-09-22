using NovaLeave.Domain.Entities;

namespace NovaLeave.Domain.Tests;

public class EmployeeBalanceTests
{
    private static Employee CreateEmployee(int initialBalance = 20) =>
        new(
            id: Guid.NewGuid(),
            identityId: "identity-1",
            name: "Test",
            email: "test@example.com",
            employmentStartDate: new DateOnly(2024, 1, 1),
            initialBalance: initialBalance,
            assignedApproverId: null);

    [Fact]
    public void ReserveDays_reducesBalance()
    {
        var employee = CreateEmployee(20);

        employee.ReserveDays(5);

        Assert.Equal(15, employee.Balance);
    }

    [Fact]
    public void ReserveDays_moreThanBalance_throws()
    {
        var employee = CreateEmployee(3);

        Assert.Throws<InvalidOperationException>(() => employee.ReserveDays(5));
    }

    [Fact]
    public void ReserveDays_zeroOrNegative_throws()
    {
        var employee = CreateEmployee();

        Assert.Throws<InvalidOperationException>(() => employee.ReserveDays(0));
        Assert.Throws<InvalidOperationException>(() => employee.ReserveDays(-1));
    }

    [Fact]
    public void DeductDays_reducesBalance()
    {
        var employee = CreateEmployee(10);

        employee.DeductDays(3);

        Assert.Equal(7, employee.Balance);
    }

    [Fact]
    public void RestoreDays_addsToBalance()
    {
        var employee = CreateEmployee(5);
        employee.ReserveDays(4);

        employee.RestoreDays(4);

        Assert.Equal(5, employee.Balance);
    }

    [Fact]
    public void SetAssignedApprover_selfAssignment_throws()
    {
        var employee = CreateEmployee();

        Assert.Throws<InvalidOperationException>(() => employee.SetAssignedApprover(employee.Id));
    }

    [Fact]
    public void ReserveDays_inactiveAccount_throws()
    {
        var employee = CreateEmployee();
        employee.Deactivate();

        Assert.Throws<InvalidOperationException>(() => employee.ReserveDays(1));
    }

    [Fact]
    public void BalanceIsNeverNegative()
    {
        var employee = CreateEmployee(2);

        try
        {
            employee.ReserveDays(3);
        }
        catch (InvalidOperationException)
        {
            // esperado
        }

        Assert.True(employee.Balance >= 0);
    }
}
