using LandingPage.Models;

namespace LandingPage.Services;

public class DayOfWeekRouter : IDayOfWeekRouter
{
    // xkcd publishes on Mon, Wed, Fri (but we'll use Tue, Thu, Sat for xkcd days)
    // APOD gets the remaining days: Sun, Mon, Wed, Fri
    private static readonly HashSet<DayOfWeek> XkcdDays = new()
    {
        DayOfWeek.Tuesday,
        DayOfWeek.Thursday,
        DayOfWeek.Saturday
    };

    public RedirectTarget GetRedirectTarget(DateTime dateTime)
    {
        return XkcdDays.Contains(dateTime.DayOfWeek)
            ? RedirectTarget.Xkcd
            : RedirectTarget.Apod;
    }
}
