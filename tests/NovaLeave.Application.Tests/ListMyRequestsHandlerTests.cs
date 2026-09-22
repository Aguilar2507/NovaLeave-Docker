using NSubstitute;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Application.Features.VacationRequest.List;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Tests;

// T500: Handler tests for ListMyRequestsHandler (US5, Phase 7).
// Validates own-requests-only filtering, pagination behavior.
public class ListMyRequestsHandlerTests
{
    private readonly IVacationRequestRepository _mockRepository;
    private static readonly Guid TestEmployeeId = Guid.NewGuid();
    private static readonly Guid OtherEmployeeId = Guid.NewGuid();

    public ListMyRequestsHandlerTests()
    {
        _mockRepository = Substitute.For<IVacationRequestRepository>();
    }

    private static Employee CreateTestEmployee(Guid id, int balance) =>
        new(id, $"test-{id}", $"Test Emp {id}", $"test{id}@test.com",
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1), balance, null);

    private static VacationRequest CreateTestRequest(Guid requestId, Guid ownerId, DateOnly start, RequestStatus status)
    {
        var end = start.AddDays(4);
        var request = new VacationRequest(requestId, ownerId, start, end, "Test vacation", 5, end.AddDays(30));

        // Transition to desired status
        if (status == RequestStatus.Approved)
        {
            request.Approve(Guid.NewGuid());
        }
        else if (status == RequestStatus.Rejected)
        {
            request.Reject(Guid.NewGuid(), "Test rejection");
        }
        else if (status == RequestStatus.Cancelled)
        {
            request.Cancel();
        }
        else if (status == RequestStatus.Voided)
        {
            request.Approve(Guid.NewGuid());
            request.Void(ownerId, "Test void");
        }

        return request;
    }

    [Fact]
    public async Task Handle_ReturnsOnlyOwnRequests()
    {
        // Arrange
        var handler = new ListMyRequestsHandler(_mockRepository);
        var employee = CreateTestEmployee(TestEmployeeId, 15);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ownRequests = new List<VacationRequest>
        {
            CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(10), RequestStatus.Pending),
            CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(20), RequestStatus.Approved),
            CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(30), RequestStatus.Rejected)
        };

        var otherRequests = new List<VacationRequest>
        {
            CreateTestRequest(Guid.NewGuid(), OtherEmployeeId, today.AddDays(15), RequestStatus.Pending)
        };

        var allRequests = ownRequests.Concat(otherRequests).ToList();

        _mockRepository.GetByOwnerAsync(TestEmployeeId, Arg.Any<CancellationToken>())
            .Returns(ownRequests);

        var query = new ListMyRequestsQuery(Limit: 10, Offset: 0);

        // Act
        var result = await handler.Handle(query, employee, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Requests.Count);
        Assert.All(result.Requests, r => Assert.Equal(TestEmployeeId, r.OwnerId));
    }

    [Fact]
    public async Task Handle_IncludesCurrentBalance()
    {
        // Arrange
        var handler = new ListMyRequestsHandler(_mockRepository);
        var employee = CreateTestEmployee(TestEmployeeId, 15);

        _mockRepository.GetByOwnerAsync(TestEmployeeId, Arg.Any<CancellationToken>())
            .Returns(new List<VacationRequest>());

        var query = new ListMyRequestsQuery(Limit: 10, Offset: 0);

        // Act
        var result = await handler.Handle(query, employee, CancellationToken.None);

        // Assert
        Assert.Equal(15, result.CurrentBalance);
    }

    [Fact]
    public async Task Handle_RespectsPaginationLimit()
    {
        // Arrange
        var handler = new ListMyRequestsHandler(_mockRepository);
        var employee = CreateTestEmployee(TestEmployeeId, 15);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var requests = Enumerable.Range(0, 25)
            .Select(i => CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays((i + 1) * 5), RequestStatus.Pending))
            .ToList();

        _mockRepository.GetByOwnerAsync(TestEmployeeId, Arg.Any<CancellationToken>())
            .Returns(requests);

        var query = new ListMyRequestsQuery(Limit: 10, Offset: 0);

        // Act
        var result = await handler.Handle(query, employee, CancellationToken.None);

        // Assert
        Assert.Equal(10, result.Requests.Count);
        Assert.Equal(25, result.TotalCount);
    }

    [Fact]
    public async Task Handle_SupportsOffset()
    {
        // Arrange
        var handler = new ListMyRequestsHandler(_mockRepository);
        var employee = CreateTestEmployee(TestEmployeeId, 15);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var requests = Enumerable.Range(0, 25)
            .Select(i => CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays((i + 1) * 5), RequestStatus.Pending))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        _mockRepository.GetByOwnerAsync(TestEmployeeId, Arg.Any<CancellationToken>())
            .Returns(requests);

        var query = new ListMyRequestsQuery(Limit: 10, Offset: 10);

        // Act
        var result = await handler.Handle(query, employee, CancellationToken.None);

        // Assert
        Assert.Equal(10, result.Requests.Count);
        Assert.Equal(25, result.TotalCount);
        // Verify we got the second page (items 10-19)
        Assert.Equal(requests[10].Id, result.Requests[0].Id);
    }

    [Fact]
    public async Task Handle_OrdersByCreatedAtDescending()
    {
        // Arrange
        var handler = new ListMyRequestsHandler(_mockRepository);
        var employee = CreateTestEmployee(TestEmployeeId, 15);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var oldRequest = CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(10), RequestStatus.Pending);
        var newRequest = CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(20), RequestStatus.Pending);

        // Simulate repository returning in creation order
        var requests = new List<VacationRequest> { oldRequest, newRequest };

        _mockRepository.GetByOwnerAsync(TestEmployeeId, Arg.Any<CancellationToken>())
            .Returns(requests);

        var query = new ListMyRequestsQuery(Limit: 10, Offset: 0);

        // Act
        var result = await handler.Handle(query, employee, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Requests.Count);
        // Most recent first (by CreatedAt, which is set on construction)
        Assert.True(result.Requests[0].CreatedAt >= result.Requests[1].CreatedAt);
    }

    [Fact]
    public async Task Handle_IncludesAllStatusTypes()
    {
        // Arrange
        var handler = new ListMyRequestsHandler(_mockRepository);
        var employee = CreateTestEmployee(TestEmployeeId, 15);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var requests = new List<VacationRequest>
        {
            CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(10), RequestStatus.Pending),
            CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(20), RequestStatus.Approved),
            CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(30), RequestStatus.Rejected),
            CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(40), RequestStatus.Cancelled),
            CreateTestRequest(Guid.NewGuid(), TestEmployeeId, today.AddDays(50), RequestStatus.Voided)
        };

        _mockRepository.GetByOwnerAsync(TestEmployeeId, Arg.Any<CancellationToken>())
            .Returns(requests);

        var query = new ListMyRequestsQuery(Limit: 10, Offset: 0);

        // Act
        var result = await handler.Handle(query, employee, CancellationToken.None);

        // Assert
        Assert.Equal(5, result.Requests.Count);
        Assert.Contains(result.Requests, r => r.Status == RequestStatus.Pending);
        Assert.Contains(result.Requests, r => r.Status == RequestStatus.Approved);
        Assert.Contains(result.Requests, r => r.Status == RequestStatus.Rejected);
        Assert.Contains(result.Requests, r => r.Status == RequestStatus.Cancelled);
        Assert.Contains(result.Requests, r => r.Status == RequestStatus.Voided);
    }
}
