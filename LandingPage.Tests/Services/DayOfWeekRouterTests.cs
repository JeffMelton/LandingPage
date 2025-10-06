using LandingPage.Models;
using LandingPage.Services;
using Xunit;

namespace LandingPage.Tests.Services;

public class DayOfWeekRouterTests
{
    private readonly DayOfWeekRouter _router = new();

    [Theory]
    [InlineData("2025-01-07", RedirectTarget.Xkcd)]  // Tuesday
    [InlineData("2025-01-09", RedirectTarget.Xkcd)]  // Thursday
    [InlineData("2025-01-11", RedirectTarget.Xkcd)]  // Saturday
    public void GetRedirectTarget_XkcdDays_ReturnsXkcd(string dateString, RedirectTarget expected)
    {
        // Arrange
        var date = DateTime.Parse(dateString);

        // Act
        var result = _router.GetRedirectTarget(date);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2025-01-05", RedirectTarget.Apod)]  // Sunday
    [InlineData("2025-01-06", RedirectTarget.Apod)]  // Monday
    [InlineData("2025-01-08", RedirectTarget.Apod)]  // Wednesday
    [InlineData("2025-01-10", RedirectTarget.Apod)]  // Friday
    public void GetRedirectTarget_ApodDays_ReturnsApod(string dateString, RedirectTarget expected)
    {
        // Arrange
        var date = DateTime.Parse(dateString);

        // Act
        var result = _router.GetRedirectTarget(date);

        // Assert
        Assert.Equal(expected, result);
    }
}
