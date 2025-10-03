using LandingPage.Models;

namespace LandingPage.Services;

public interface IApodSelector
{
    ApodEntry SelectEntry(IReadOnlyList<ApodEntry> entries, DateTime seedDate);
}
