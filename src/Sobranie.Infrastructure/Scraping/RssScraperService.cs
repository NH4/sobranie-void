using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sobranie.Infrastructure.Persistence;

namespace Sobranie.Infrastructure.Scraping;

public sealed partial class RssScraperService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScraperOptions _scraper;
    private readonly ILogger<RssScraperService> _logger;

    public RssScraperService(
        IServiceScopeFactory scopeFactory,
        IOptions<SobranieOptions> options,
        ILogger<RssScraperService> logger)
    {
        _scopeFactory = scopeFactory;
        _scraper = options.Value.Scraper;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "RssScraperService starting. Feed={FeedUrl}, Interval={Interval}m.")]
    private partial void LogStarted(string feedUrl, int interval);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RSS fetch failed: {Reason}.")]
    private partial void LogFetchError(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "RSS scraped {New_}/{Total} new items from {Source}.")]
    private partial void LogScraped(int New_, int total, string source);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_scraper.RssFeedUrl, _scraper.PollIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScrapeAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogFetchError(ex.Message);
            }

            await Task.Delay(TimeSpan.FromMinutes(_scraper.PollIntervalMinutes), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ScrapeAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SobranieDbContext>();

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        var xml = await http.GetStringAsync(_scraper.RssFeedUrl, ct).ConfigureAwait(false);

        var doc = XDocument.Parse(xml);
        var items = doc.Descendants("item").ToList();

        var newCount = 0;

        foreach (var item in items)
        {
            var titleEl = item.Element("title");
            var linkEl = item.Element("link");
            var categoryEl = item.Element("category");
            var pubDateEl = item.Element("pubDate");

            if (titleEl is null || linkEl is null)
            {
                continue;
            }

            var rawTitle = titleEl.Value.Trim();
            var (headline, source) = SplitHeadlineAndSource(rawTitle);
            var url = linkEl.Value.Trim();
            var tag = categoryEl?.Value.Trim() ?? string.Empty;

            var exists = await db.Proposals
                .AsNoTracking()
                .AnyAsync(p => p.SourceUrl == url, ct)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            db.Proposals.Add(new Domain.Proposal
            {
                SourceUrl = url,
                Source = source,
                Headline = headline,
                FetchedAt = DateTimeOffset.UtcNow,
                Status = Domain.ProposalStatus.Queued,
            });

            newCount++;
        }

        if (newCount > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        var topSource = items.FirstOrDefault()?.Element("title")?.Value.Trim() ?? _scraper.RssFeedUrl;
        var displaySource = ExtractDomain(topSource);
        LogScraped(newCount, items.Count, displaySource);
    }

    private static (string Headline, string Source) SplitHeadlineAndSource(string raw)
    {
        var parts = raw.Split(" | ", 2);
        if (parts.Length == 2)
        {
            return (parts[0].Trim(), parts[1].Trim());
        }

        return (raw, ExtractDomain(raw));
    }

    private static string ExtractDomain(string text)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.AsSpan().TrimStart("www.").ToString();
            return host;
        }

        var truncated = text.Length > 30 ? text[..30] : text;
        return truncated.Length == text.Length ? text : truncated + "…";
    }
}