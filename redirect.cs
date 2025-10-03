using LandingPage.Models;
using LandingPage.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LandingPage;

public class redirect
{
    private readonly ILogger<redirect> _logger;
    private readonly IDayOfWeekRouter _router;
    private readonly IApodArchiveScraper _scraper;
    private readonly IApodSelector _selector;

    public redirect(
        ILogger<redirect> logger,
        IDayOfWeekRouter router,
        IApodArchiveScraper scraper,
        IApodSelector selector)
    {
        _logger = logger;
        _router = router;
        _scraper = scraper;
        _selector = selector;
    }

    [Function("Home")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*url}")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing redirect request");

        try
        {
            var now = DateTime.UtcNow;
            var target = _router.GetRedirectTarget(now);

            if (target == RedirectTarget.Xkcd)
            {
                _logger.LogInformation("Redirecting to xkcd");
                return new RedirectResult("https://xkcd.com", permanent: false);
            }

            // APOD redirect
            _logger.LogInformation("Fetching APOD archive for redirect");
            var entries = await _scraper.GetArchiveEntriesAsync(cancellationToken);

            if (entries.Count == 0)
            {
                _logger.LogError("No APOD entries available");
                return new ContentResult
                {
                    Content = "Service temporarily unavailable - no APOD entries found",
                    StatusCode = 503,
                    ContentType = "text/plain"
                };
            }

            var selected = _selector.SelectEntry(entries, now);
            _logger.LogInformation("Redirecting to APOD: {Url} (Date: {Date})", selected.Url, selected.Date);

            return new RedirectResult(selected.Url, permanent: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing redirect");
            return new ContentResult
            {
                Content = "Service temporarily unavailable",
                StatusCode = 503,
                ContentType = "text/plain"
            };
        }
    }
}
