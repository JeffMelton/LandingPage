using LandingPage.Models;

namespace LandingPage.Services;

public interface IDayOfWeekRouter
{
    RedirectTarget GetRedirectTarget(DateTime dateTime);
}
