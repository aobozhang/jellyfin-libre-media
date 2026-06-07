using LibreMedia.Models;
using LibreMedia.Services;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace LibreMedia.Channels;

public class LibreChannel(
    ContentSource source,
    ILogger<LibreChannel> logger) : Channel, IChannel
{
    private readonly ContentSource _source = source;
    private readonly ILogger<LibreChannel> _logger = logger;

    public override string Name => _source.Name;

    public string Description => string.Empty;
    public string DataVersion => "1";
    public new string HomePageUrl => string.Empty;
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    private ContentSyncService SyncService => ServiceLocator.SyncService;
    private StreamResolverService StreamResolver => ServiceLocator.StreamResolver;

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken ct)
    {
        var items = SyncService.GetCachedItems(_source.Id);
        var folderId = query.FolderId ?? string.Empty;

        var result = folderId switch
        {
            "" => BuildRootFolders(items),
            "latest" => BuildLatestItems(items, query),
            _ when folderId.StartsWith("cat_") => BuildCategoryItems(items, query, folderId),
            _ => BuildRootFolders(items)
        };

        return new ChannelItemResult { Items = result };
    }

    private List<ChannelItemInfo> BuildRootFolders(List<CmsVideoItem> items)
    {
        var results = new List<ChannelItemInfo>
        {
            new()
            {
                Name = "最新",
                Id = "latest",
                Type = ChannelItemType.Folder,
                MediaType = ChannelMediaType.Video,
                ImageUrl = null
            }
        };

        var categories = items.GroupBy(x => (x.Type, x.TypeName))
            .Where(g => !string.IsNullOrEmpty(g.Key.TypeName))
            .OrderBy(g => g.Key.Type);

        foreach (var cat in categories)
        {
            results.Add(new ChannelItemInfo
            {
                Name = cat.Key.TypeName,
                Id = $"cat_{cat.Key.Type}",
                Type = ChannelItemType.Folder,
                MediaType = ChannelMediaType.Video,
                ImageUrl = null
            });
        }

        return results;
    }

    private List<ChannelItemInfo> BuildLatestItems(List<CmsVideoItem> items, InternalChannelItemQuery query)
    {
        return items
            .OrderByDescending(x => x.UpdateTime)
            .Skip(query.StartIndex ?? 0)
            .Take(query.Limit ?? 20)
            .Select(BuildMediaItem)
            .ToList();
    }

    private List<ChannelItemInfo> BuildCategoryItems(List<CmsVideoItem> items, InternalChannelItemQuery query, string folderId)
    {
        if (!int.TryParse(folderId.AsSpan(4), out var typeId))
            return [];

        var filtered = items.Where(x => x.Type == typeId);

        return filtered
            .Skip(query.StartIndex ?? 0)
            .Take(query.Limit ?? 20)
            .Select(BuildMediaItem)
            .ToList();
    }

    private ChannelItemInfo BuildMediaItem(CmsVideoItem item)
    {
        var mediaSources = new List<MediaSourceInfo>();
        if (!string.IsNullOrEmpty(item.PlayUrl))
        {
            mediaSources.Add(new MediaSourceInfo
            {
                Id = item.Id.ToString(),
                Path = StreamResolver.ResolveStreamUrl(item),
                Protocol = MediaProtocol.Http
            });
        }

        return new ChannelItemInfo
        {
            Name = item.Name,
            Id = $"item_{item.Id}",
            Type = ChannelItemType.Media,
            MediaType = ChannelMediaType.Video,
            ImageUrl = item.Pic,
            MediaSources = mediaSources,
            ProviderIds = new Dictionary<string, string>
            {
                ["cms_id"] = item.Id.ToString()
            }
        };
    }

    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            MediaTypes = [ChannelMediaType.Video],
            ContentTypes = [ChannelMediaContentType.Movie],
            MaxPageSize = 50,
            SupportsContentDownloading = false
        };
    }

    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken ct)
    {
        return Task.FromResult(new DynamicImageResponse { HasImage = false });
    }

    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return [ImageType.Primary];
    }

    public bool IsEnabledFor(string userId) => _source.IsEnabled;
}