using System.Text.RegularExpressions;
using LandingPage.Services;
using Xunit;

namespace LandingPage.Tests.Services;

public class ApodArchiveScraperTests
{
    [Theory]
    [InlineData("2024 December 31", 2024, 12, 31)]
    [InlineData("2024 January 1", 2024, 1, 1)]
    [InlineData("2025 October 6", 2025, 10, 6)]
    [InlineData("2023 February 14", 2023, 2, 14)]
    public void TryParseApodDate_ValidFormats_ReturnsTrue(string input, int year, int month, int day)
    {
        // Act
        var result = ApodArchiveScraper.TryParseApodDate(input, out var date);

        // Assert
        Assert.True(result);
        Assert.Equal(new DateOnly(year, month, day), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("2024-12-31")]
    [InlineData("December 31, 2024")]
    [InlineData("31 December 2024")]
    public void TryParseApodDate_InvalidFormats_ReturnsFalse(string input)
    {
        // Act
        var result = ApodArchiveScraper.TryParseApodDate(input, out var date);

        // Assert
        Assert.False(result);
        Assert.Equal(default, date);
    }

    [Theory]
    [InlineData("ap241231.html")]
    [InlineData("ap250101.html")]
    [InlineData("ap000101.html")]
    [InlineData("ap991231.html")]
    public void ApodLinkPattern_ValidLinks_Matches(string link)
    {
        // Arrange
        var pattern = @"^ap\d{6}\.html$";
        var regex = new Regex(pattern);

        // Act
        var result = regex.IsMatch(link);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("ap2412.html")]           // Too few digits
    [InlineData("ap24123100.html")]       // Too many digits
    [InlineData("ap241231.htm")]          // Wrong extension
    [InlineData("ap241231")]              // No extension
    [InlineData("241231.html")]           // Missing 'ap' prefix
    [InlineData("apodlink.html")]         // Not digits
    public void ApodLinkPattern_InvalidLinks_DoesNotMatch(string link)
    {
        // Arrange
        var pattern = @"^ap\d{6}\.html$";
        var regex = new Regex(pattern);

        // Act
        var result = regex.IsMatch(link);

        // Assert
        Assert.False(result);
    }
}
