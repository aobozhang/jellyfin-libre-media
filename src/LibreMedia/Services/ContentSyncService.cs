using LibreMedia.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibreMedia.Services;

public class ContentSyncService(
    ILogger<ContentSyncService> logger,
    CmsApiClient cmsApiClient) : IHostedService, IDisposable
{
    private readonly ILogger<ContentSyncService> _logger = logger;
    private readonly CmsApiClient _cmsApiClient = cmsApiClient;
    private Timer? _timer;
    private bool _disposed;

    private readonly Dictionary<string, List<CmsVideoItem>> _cache = [];
    private readonly Dictionary<string, List<CmsSubSource>> _subSourceCache = [];
    private readonly object _cacheLock = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ContentSyncService starting");
        _timer = new Timer(async _ => await SyncAllSourcesAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(30));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ContentSyncService stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public async Task SyncAllSourcesAsync(CancellationToken ct = default)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null) return;

        var tasks = config.ContentSources
            .Where(s => s.IsEnabled)
            .Select(s => SyncSourceAsync(s, ct));
        await Task.WhenAll(tasks);
    }

    public async Task SyncSourceAsync(ContentSource source, CancellationToken ct = default)
    {
        _logger.LogInformation("Syncing source {Name}", source.Name);
        try
        {
            // First fetch to get sub-sources and first page of content
            var firstPage = await _cmsApiClient.GetContentAsync(source.ApiUrl, 1, 50, ct);
            if (firstPage == null) return;

            lock (_cacheLock)
            {
                _subSourceCache[source.Id] = [];
            }

            // If this source has api_site sub-sources, enumerate them
            if (firstPage.ApiSite?.Count > 0)
            {
                var subSources = firstPage.ApiSite.Select(kv => new CmsSubSource
                {
                    Name = kv.Value.Name,
                    Api = kv.Value.Api,
                    Detail = kv.Value.Detail
                }).ToList();

                lock (_cacheLock)
                {
                    _subSourceCache[source.Id] = subSources;
                }
                _logger.LogInformation("Found {Count} sub-sources in {Name}", subSources.Count, source.Name);

                // Sync each sub-source in parallel
                var subTasks = subSources.Select((sub, idx) =>
                    SyncSubSourceAsync(source, idx.ToString(), sub.Api, ct)).ToList();
                await Task.WhenAll(subTasks);
            }
            else
            {
                // No sub-sources, treat source itself as content source
                var items = new List<CmsVideoItem>();
                if (firstPage.List?.Count > 0)
                    items.AddRange(firstPage.List);

                // Fetch remaining pages
                int page = 2;
                while (true)
                {
                    var more = await _cmsApiClient.GetContentAsync(source.ApiUrl, page, 50, ct);
                    if (more?.List == null || more.List.Count == 0) break;
                    items.AddRange(more.List);
                    if (more.List.Count < 50) break;
                    page++;
                    if (page > 100) break;
                }

                lock (_cacheLock)
                {
                    _cache[$"{source.Id}:0"] = items;
                }
                _logger.LogInformation("Synced {Count} items directly from {Name}", items.Count, source.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync source {Name}", source.Name);
        }
    }

    private async Task SyncSubSourceAsync(ContentSource source, string subIndex, string subApiUrl, CancellationToken ct)
    {
        try
        {
            var items = await _cmsApiClient.GetAllContentAsync(subApiUrl, ct);
            lock (_cacheLock)
            {
                _cache[$"{source.Id}:{subIndex}"] = items;
            }
            _logger.LogInformation("Synced sub-source {SubIndex} from {Name}: {Count} items", subIndex, source.Name, items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync sub-source {SubIndex} from {Name}", subIndex, source.Name);
        }
    }

    public List<CmsVideoItem> GetCachedItems(string sourceId, string subIndex = "0")
    {
        lock (_cacheLock)
        {
            var key = $"{sourceId}:{subIndex}";
            return _cache.TryGetValue(key, out var items) ? items : [];
        }
    }

    public List<CmsSubSource> GetCachedSubSources(string sourceId)
    {
        lock (_cacheLock)
        {
            return _subSourceCache.TryGetValue(sourceId, out var sources) ? sources : [];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}