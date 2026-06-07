using Microsoft.Extensions.Logging;

namespace LibreMedia.Services;

public static class ServiceLocator
{
    private static CmsApiClient? _cmsApiClient;
    private static ContentSyncService? _syncService;
    private static StreamResolverService? _streamResolver;

    public static void Initialize()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger<CmsApiClient>();
        _cmsApiClient = new CmsApiClient(new HttpClient(), logger);
        _streamResolver = new StreamResolverService();
        _syncService = new ContentSyncService(
            loggerFactory.CreateLogger<ContentSyncService>(),
            _cmsApiClient);
    }

    public static CmsApiClient CmsApiClient
    {
        get
        {
            if (_cmsApiClient is null) Initialize();
            return _cmsApiClient!;
        }
    }

    public static ContentSyncService SyncService
    {
        get
        {
            if (_syncService is null) Initialize();
            return _syncService!;
        }
    }

    public static StreamResolverService StreamResolver
    {
        get
        {
            if (_streamResolver is null) Initialize();
            return _streamResolver!;
        }
    }
}