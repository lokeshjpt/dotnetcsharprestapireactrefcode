namespace PWA.PermitsApi.Application.Common;

/// <summary>
/// Alameda County observed holidays and weekend detection, ported from the legacy JBoss ecomm
/// <c>CountyHolidays</c> / <c>BeanCalendar.isWeekend</c>. Days that are weekends or observed County
/// holidays are not available for inspection, so they gate valid project start/completion dates.
/// The observed-date shifting mirrors the legacy switch tables exactly (including Veterans Day, which
/// the legacy code observes on the following Monday when Nov 11 falls on a Saturday).
/// </summary>
public static class CountyHolidays
{
    public static bool IsWeekend(DateTime date)
        => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

    public static bool IsCountyHoliday(DateTime date)
        => ObservedHolidays(date.Year).Contains(date.Date);

    private static HashSet<DateTime> ObservedHolidays(int year)
    {
        var thanksgivingThursday = NthWeekday(year, 11, DayOfWeek.Thursday, 4);
        return new HashSet<DateTime>
        {
            ObservedFixed(new DateTime(year, 1, 1)),          // New Year's Day
            NthWeekday(year, 1, DayOfWeek.Monday, 3),         // Martin Luther King Jr. Day (3rd Mon Jan)
            ObservedFixed(new DateTime(year, 2, 12)),         // Lincoln's Day (Feb 12)
            NthWeekday(year, 2, DayOfWeek.Monday, 3),         // Presidents' Day (3rd Mon Feb)
            LastWeekday(year, 5, DayOfWeek.Monday),           // Memorial Day (last Mon May)
            ObservedFixed(new DateTime(year, 7, 4)),          // Independence Day
            NthWeekday(year, 9, DayOfWeek.Monday, 1),         // Labor Day (1st Mon Sep)
            ObservedVeterans(year),                            // Veterans Day (Nov 11)
            thanksgivingThursday,                              // Thanksgiving (4th Thu Nov)
            thanksgivingThursday.AddDays(1),                   // Day after Thanksgiving
            ObservedFixed(new DateTime(year, 12, 25)),        // Christmas Day
            // When Jan 1 of next year is a Saturday it is observed on Dec 31 of this year.
            ObservedFixed(new DateTime(year + 1, 1, 1)),
        };
    }

    private static DateTime ObservedFixed(DateTime date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(-1),
        DayOfWeek.Sunday => date.AddDays(1),
        _ => date,
    };

    private static DateTime ObservedVeterans(int year)
    {
        var date = new DateTime(year, 11, 11);
        return date.DayOfWeek switch
        {
            DayOfWeek.Sunday => date.AddDays(1),    // observed Monday Nov 12
            DayOfWeek.Saturday => date.AddDays(2),  // legacy observes Monday Nov 13
            _ => date,
        };
    }

    private static DateTime NthWeekday(int year, int month, DayOfWeek dayOfWeek, int occurrence)
    {
        var first = new DateTime(year, month, 1);
        var offset = ((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + 7 * (occurrence - 1));
    }

    private static DateTime LastWeekday(int year, int month, DayOfWeek dayOfWeek)
    {
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        var offset = ((int)last.DayOfWeek - (int)dayOfWeek + 7) % 7;
        return last.AddDays(-offset);
    }
}
