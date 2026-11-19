using System.Net;
using Api.Data;
using Api.Models;
using Api.Services;

namespace Api.Tests;

/// <summary>Days taken this year (employee summary card): only Approved leave counts.</summary>
public class LeaveSummaryTests
{
    static LeaveRequest TwoDays(int employeeId, LeaveStatus status, HalfDay halfDay = HalfDay.None)
    {
        var monday = Seed.MondayOnOrAfter(new DateOnly(DateTime.UtcNow.Year, 3, 2));
        var end = halfDay == HalfDay.None ? monday.AddDays(1) : monday;
        return new LeaveRequest { EmployeeId = employeeId, StartDate = monday, EndDate = end, HalfDay = halfDay, Status = status, Reason = "Summary test" };
    }

    static async Task<decimal> AddAndGetDelta(LeaveStatus status, HalfDay halfDay = HalfDay.None)
    {
        using var db = TestDb.Create();
        var service = new LeaveService(db);
        var before = await service.DaysTakenThisYearAsync(1);
        db.LeaveRequests.Add(TwoDays(1, status, halfDay));
        await db.SaveChangesAsync();
        return (await service.DaysTakenThisYearAsync(1))!.Value - before!.Value;
    }

    [Fact]
    public async Task Approved_leave_counts_as_taken() =>
        Assert.Equal(LeaveService.DaysOf(TwoDays(1, LeaveStatus.Approved)), await AddAndGetDelta(LeaveStatus.Approved));

    [Fact]
    public async Task Approved_half_day_counts_half() =>
        Assert.Equal(0.5m, await AddAndGetDelta(LeaveStatus.Approved, HalfDay.AM));

    [Fact]
    public async Task Pending_leave_is_not_taken() =>
        Assert.Equal(0m, await AddAndGetDelta(LeaveStatus.Pending));

    [Fact]
    public async Task Rejected_leave_is_not_taken() =>
        Assert.Equal(0m, await AddAndGetDelta(LeaveStatus.Rejected));

    [Fact]
    public async Task Unknown_employee_has_no_summary()
    {
        using var db = TestDb.Create();
        Assert.Null(await new LeaveService(db).DaysTakenThisYearAsync(999));
    }
}

public class LeaveSummaryApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Employee_cannot_see_someone_elses_summary()
    {
        var farah = await api.SignedInAs(People.Farah);
        Assert.Equal(HttpStatusCode.OK, (await farah.GetAsync("/employees/6/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farah.GetAsync("/employees/7/summary")).StatusCode);
    }

    [Fact]
    public async Task Unknown_employee_summary_is_404()
    {
        var hr = await api.SignedInAs(People.Dina);
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync("/employees/99999/summary")).StatusCode);
    }
}
