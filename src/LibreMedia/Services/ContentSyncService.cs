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
    private readonly object _cacheLock = new();

    public IReadOnlyDictionary<string, List<CmsVideoItem>> Cache
    {
        get
        {
            lock (_cacheLock) return new Dictionary<string, List<CmsVideoItem>>(_cache);
        }
    }

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
            var items = await _cmsApiClient.GetAllContentAsync(source.ApiUrl, ct);
            lock (_cacheLock)
            {
                _cache[source.Id] = items;
            }
            _logger.LogInformation("Synced {Count} items from {Name}", items.Count, source.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync source {Name}", source.Name);
        }
    }

    public List<CmsVideoItem> GetCachedItems(string sourceId)
    {
        lock (_cacheLock)
        {
            return _cache.TryGetValue(sourceId, out var items) ? items : [];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}