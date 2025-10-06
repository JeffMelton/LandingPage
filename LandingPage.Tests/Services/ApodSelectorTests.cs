using LandingPage.Models;
using LandingPage.Services;
using Xunit;

namespace LandingPage.Tests.Services;

public class ApodSelectorTests
{
    private readonly ApodSelector _selector = new();

    [Fact]
    public void SelectEntry_SameDate_ReturnsSameEntry()
    {
        // Arrange
        var entries = CreateTestEntries(10);
        var date = new DateTime(2025, 1, 15);

        // Act
        var first = _selector.SelectEntry(entries, date);
        var second = _selector.SelectEntry(entries, date);

        // Assert
        Assert.Equal(first.Url, second.Url);
        Assert.Equal(first.Date, second.Date);
    }

    [Fact]
    public void SelectEntry_DifferentDates_CanReturnDifferentEntries()
    {
        // Arrange
        var entries = CreateTestEntries(100);
        var date1 = new DateTime(2025, 1, 15);
        var date2 = new DateTime(2025, 1, 16);

        // Act
        var selected1 = _selector.SelectEntry(entries, date1);
        var selected2 = _selector.SelectEntry(entries, date2);

        // Assert
        // Note: This is probabilistic - with 100 entries, highly likely to differ
        // but not guaranteed. If this flakes, consider it acceptable.
        Assert.NotEqual(selected1.Url, selected2.Url);
    }

    [Fact]
    public void SelectEntry_SingleEntry_ReturnsThatEntry()
    {
        // Arrange
        var entries = CreateTestEntries(1);
        var date = new DateTime(2025, 1, 15);

        // Act
        var result = _selector.SelectEntry(entries, date);

        // Assert
        Assert.Equal(entries[0].Url, result.Url);
    }

    [Fact]
    public void SelectEntry_EmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var entries = new List<ApodEntry>();
        var date = new DateTime(2025, 1, 15);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _selector.SelectEntry(entries, date));
    }

    private static List<ApodEntry> CreateTestEntries(int count)
    {
        var entries = new List<ApodEntry>();
        for (int i = 0; i < count; i++)
        {
            var date = new DateOnly(2024, 1, 1).AddDays(i);
            entries.Add(new ApodEntry(date, $"https://apod.nasa.gov/apod/ap{i:D6}.html"));
        }
        return entries;
    }
}
