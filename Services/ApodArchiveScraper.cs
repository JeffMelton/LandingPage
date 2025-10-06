using Azure.Storage.Blobs;
using HtmlAgilityPack;
using LandingPage.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LandingPage.Services;

public partial class ApodArchiveScraper : IApodArchiveScraper
{
    private const string ArchiveUrl = "https://apod.nasa.gov/apod/archivepix.html";
    private const string BlobContainerName = "apod-archive";
    private const string BlobName = "archive-entries.json";

    private readonly HttpClient _httpClient;
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly ILogger<ApodArchiveScraper> _logger;

    // In-memory cache (loaded once per instance lifetime)
    private List<ApodEntry>? _cachedEntries;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public ApodArchiveScraper(
        HttpClient httpClient,
        ILogger<ApodArchiveScraper> logger,
        BlobServiceClient? blobServiceClient = null)
    {
        _httpClient = httpClient;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ApodEntry>> GetArchiveEntriesAsync(CancellationToken cancellationToken = default)
    {
        // Return from in-memory cache if available
        if (_cachedEntries != null)
        {
            _logger.LogInformation("Returning in-memory cached archive entries ({Count} entries)", _cachedEntries.Count);
            return _cachedEntries;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedEntries != null)
            {
                return _cachedEntries;
            }

            List<ApodEntry> entries;

            // Try to load from blob storage first
            if (_blobServiceClient != null && await TryLoadFromBlobAsync(cancellationToken) is { } blobEntries)
            {
                _logger.LogInformation("Loaded {Count} entries from blob storage", blobEntries.Count);
                entries = blobEntries;
            }
            else
            {
                // Fall back to scraping
                _logger.LogInformation("Scraping APOD archive from {ArchiveUrl}", ArchiveUrl);
                entries = await ScrapeArchiveAsync(cancellationToken);

                // Save to blob storage if available
                if (_blobServiceClient != null)
                {
                    await SaveToBlobAsync(entries, cancellationToken);
                }
            }

            _cachedEntries = entries;
            return entries;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<List<ApodEntry>?> TryLoadFromBlobAsync(CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = _blobServiceClient!.GetBlobContainerClient(BlobContainerName);
            var blobClient = containerClient.GetBlobClient(BlobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogInformation("Blob {BlobName} does not exist yet", BlobName);
                return null;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var json = response.Value.Content.ToString();
            var entries = JsonSerializer.Deserialize<List<ApodEntry>>(json);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load archive from blob storage, will fall back to scraping");
            return null;
        }
    }

    private async Task SaveToBlobAsync(List<ApodEntry> entries, CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = _blobServiceClient!.GetBlobContainerClient(BlobContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(BlobName);
            var json = JsonSerializer.Serialize(entries);

            await blobClient.UploadAsync(
                BinaryData.FromString(json),
                overwrite: true,
                cancellationToken);

            _logger.LogInformation("Saved {Count} entries to blob storage", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save archive to blob storage");
        }
    }

    private async Task<List<ApodEntry>> ScrapeArchiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(ArchiveUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var entries = new List<ApodEntry>();

            // Find all links in the archive page
            // Format: <a href="ap241231.html">2024 December 31</a>
            var links = doc.DocumentNode.SelectNodes("//a[@href]");

            if (links == null)
            {
                _logger.LogWarning("No links found in archive page");
                return entries;
            }

            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", string.Empty);

                // Match links like "ap241231.html"
                var match = ApLinkRegex().Match(href);
                if (match.Success)
                {
                    // Date is in the parent node's text before the link
                    // Format: "2025 October 01:  <a href=\"...\">...\""
                    var parentText = link.ParentNode?.InnerText ?? string.Empty;

                    // Extract date portion (before the colon)
                    var colonIndex = parentText.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var dateText = parentText.Substring(0, colonIndex).Trim();

                        if (TryParseApodDate(dateText, out var date))
                        {
                            var fullUrl = $"https://apod.nasa.gov/apod/{href}";
                            entries.Add(new ApodEntry(date, fullUrl));
                        }
                    }
                }
            }

            _logger.LogInformation("Scraped {Count} APOD entries from archive", entries.Count);
            return entries;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while scraping APOD archive from {ArchiveUrl}", ArchiveUrl);
            return new List<ApodEntry>();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout while scraping APOD archive from {ArchiveUrl}", ArchiveUrl);
            return new List<ApodEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while scraping APOD archive");
            return new List<ApodEntry>();
        }
    }

    private static bool TryParseApodDate(string text, out DateOnly date)
    {
        date = default;

        // Expected formats: "2024 December 31" or "2024 December 1"
        // Try with leading zero first (dd)
        if (DateTime.TryParseExact(
            text,
            "yyyy MMMM dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }

        // Try without leading zero (d)
        if (DateTime.TryParseExact(
            text,
            "yyyy MMMM d",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"^ap\d{6}\.html$")]
    private static partial Regex ApLinkRegex();
}
