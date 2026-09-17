using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Services;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Tests;

public sealed class InspectionServiceTests
{
    [Fact]
    public async Task GetAvailabilityAsync_ExcludesUnavailableDaysAndBookedSlots()
    {
        var repository = new FakeInspectionRepository { MaxSlotsPerDay = 2 };
        var from = new DateTime(2026, 8, 10);
        var to = new DateTime(2026, 8, 11);
        repository.Unavailable.Add(new InspectionUnavailableDay { InspectionDate = new DateTime(2026, 8, 11) });
        repository.Assignments.Add(new Inspection { InspectionDate = new DateTime(2026, 8, 10), SlotId = 1, StatusCode = "IRSRV" });

        var service = new InspectionService(repository, new FakeApplicationRepository(), new FakePermitNotificationService(), NullLogger<InspectionService>.Instance);
        var availability = await service.GetAvailabilityAsync(from, to);

        // 2 days x 2 slots = 4 entries
        Assert.Equal(4, availability.Count);

        var day10Slot1 = availability.Single(a => a.Date == new DateTime(2026, 8, 10) && a.SlotId == 1);
        Assert.False(day10Slot1.Available); // already booked

        var day10Slot2 = availability.Single(a => a.Date == new DateTime(2026, 8, 10) && a.SlotId == 2);
        Assert.True(day10Slot2.Available);

        Assert.All(availability.Where(a => a.Date == new DateTime(2026, 8, 11)), a => Assert.False(a.Available)); // whole day unavailable
    }

    [Fact]
    public async Task GetAvailabilityAsync_FallsBackToDefaultSlotsOnDbError()
    {
        var repository = new FakeInspectionRepository { Throw = true };
        var service = new InspectionService(repository, new FakeApplicationRepository(), new FakePermitNotificationService(), NullLogger<InspectionService>.Instance);

        var day = new DateTime(2026, 8, 10);
        var availability = await service.GetAvailabilityAsync(day, day);

        // Fallback: 3 slots for the single day, all available.
        Assert.Equal(3, availability.Count);
        Assert.All(availability, a => Assert.True(a.Available));
    }

    [Fact]
    public async Task ScheduleAsync_AddsAssignmentWithReservedStatus()
    {
        var repository = new FakeInspectionRepository();
        var service = new InspectionService(repository, new FakeApplicationRepository(), new FakePermitNotificationService(), NullLogger<InspectionService>.Instance);

        var result = await service.ScheduleAsync(new ScheduleInspectionRequest
        {
            ApplicationId = "1700000000001",
            Date = new DateTime(2026, 8, 12),
            SlotId = 1
        });

        Assert.Equal("1700000000001", result.AppId);
        Assert.Equal(1, result.SlotId);
        Assert.Equal("IRSRV", result.StatusCode);
        Assert.NotNull(repository.Added);
    }

    [Fact]
    public async Task AddAsync_DefaultsToPendingStatus_SoLegacyIntraMarksInspectionsScheduled()
    {
        var repository = new FakeInspectionRepository();
        var service = new InspectionService(repository, new FakeApplicationRepository(), new FakePermitNotificationService(), NullLogger<InspectionService>.Instance);

        var result = await service.AddAsync(new AddInspectionRequest
        {
            AppId = "1700000000002",
            InspectionDate = new DateTime(2026, 8, 12),
            SlotId = 1,
            InspectorId = 4
        });

        // IPEND (not IRSRV): the legacy Java intra treats a slot as "scheduled" only when no
        // assignment is still IRSRV, so a fully-specified add must land as IPEND.
        Assert.Equal("IPEND", result.StatusCode);
    }

    [Fact]
    public async Task GetCalendarAsync_CodesWeekendsUnavailableAndMaxReached()
    {
        var repository = new FakeInspectionRepository { MaxSlotsPerDay = 2 };
        repository.Unavailable.Add(new InspectionUnavailableDay { InspectionDate = new DateTime(2026, 8, 18) });
        repository.Assignments.Add(new Inspection { InspectionDate = new DateTime(2026, 8, 19), SlotId = 1, StatusCode = "IPEND" });
        repository.Assignments.Add(new Inspection { InspectionDate = new DateTime(2026, 8, 19), SlotId = 2, StatusCode = "IPEND" });

        var service = new InspectionService(repository, new FakeApplicationRepository(), new FakePermitNotificationService(), NullLogger<InspectionService>.Instance);
        var calendar = await service.GetCalendarAsync(new DateTime(2026, 8, 15), new DateTime(2026, 8, 19));

        Assert.Equal("H", Day(calendar, 2026, 8, 15).Reason); // Saturday
        Assert.Equal("H", Day(calendar, 2026, 8, 16).Reason); // Sunday

        var monday = Day(calendar, 2026, 8, 17);
        Assert.True(monday.Available);
        Assert.Equal(string.Empty, monday.Reason);

        Assert.Equal("U", Day(calendar, 2026, 8, 18).Reason); // blocked by PWA
        Assert.Equal("M", Day(calendar, 2026, 8, 19).Reason); // max inspections reached
    }

    [Fact]
    public async Task GetCalendarAsync_CodesObservedCountyHolidayAsH()
    {
        var repository = new FakeInspectionRepository();
        var service = new InspectionService(repository, new FakeApplicationRepository(), new FakePermitNotificationService(), NullLogger<InspectionService>.Instance);

        // Jul 4 2026 falls on a Saturday, so Independence Day is observed on Friday Jul 3 (a weekday).
        var calendar = await service.GetCalendarAsync(new DateTime(2026, 7, 3), new DateTime(2026, 7, 3));

        var day = Assert.Single(calendar.Days);
        Assert.False(day.Available);
        Assert.Equal("H", day.Reason);
    }

    [Fact]
    public async Task GetCalendarAsync_ReturnsTenToNinetyDayValidRange()
    {
        var repository = new FakeInspectionRepository();
        var service = new InspectionService(repository, new FakeApplicationRepository(), new FakePermitNotificationService(), NullLogger<InspectionService>.Instance);

        var today = DateTime.Today;
        var calendar = await service.GetCalendarAsync(today, today.AddDays(1));

        Assert.Equal(today.AddDays(10), calendar.ValidRangeStart);
        Assert.Equal(today.AddDays(90), calendar.ValidRangeEnd);
    }

    private static InspectionDayDto Day(InspectionCalendarDto calendar, int year, int month, int day)
        => calendar.Days.Single(d => d.Date == new DateTime(year, month, day));
}
