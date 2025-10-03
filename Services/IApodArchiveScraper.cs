using LandingPage.Models;

namespace LandingPage.Services;

public interface IApodArchiveScraper
{
    Task<IReadOnlyList<ApodEntry>> GetArchiveEntriesAsync(CancellationToken cancellationToken = default);
}
