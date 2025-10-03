using LandingPage.Models;

namespace LandingPage.Services;

public class ApodSelector : IApodSelector
{
    public ApodEntry SelectEntry(IReadOnlyList<ApodEntry> entries, DateTime seedDate)
    {
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("No APOD entries available for selection");
        }

        // Use date as seed for deterministic "randomness"
        // Same day = same selection
        var seed = seedDate.Year * 10000 + seedDate.Month * 100 + seedDate.Day;
        var random = new Random(seed);

        var index = random.Next(entries.Count);
        return entries[index];
    }
}
