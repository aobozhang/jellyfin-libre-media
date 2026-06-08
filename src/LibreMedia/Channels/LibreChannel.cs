using LibreMedia.Models;
using LibreMedia.Services;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace LibreMedia.Channels;

public class LibreChannel(ILoggerFactory loggerFactory) : Channel, IChannel
{
    private readonly ILogger<LibreChannel> _logger = loggerFactory.CreateLogger<LibreChannel>();

    public override string Name => "LibreMedia";

    public string Description => string.Empty;
    public string DataVersion => "1";
    public new string HomePageUrl => string.Empty;
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    private ContentSyncService SyncService => ServiceLocator.SyncService;
    private StreamResolverService StreamResolver => ServiceLocator.StreamResolver;

    private List<ContentSource> GetEnabledSources()
        => Plugin.Instance?.Configuration?.ContentSources?.Where(s => s.IsEnabled).ToList() ?? [];

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken ct)
    {
        var folderId = query.FolderId ?? string.Empty;

        List<ChannelItemInfo> result;
        if (folderId == "")
        {
            result = BuildSourceList();
        }
        else if (folderId.StartsWith("src_"))
        {
            result = await GetSourceItemsAsync(folderId, query, ct);
        }
        else
        {
            result = BuildSourceList();
        }

        return new ChannelItemResult { Items = result };
    }

    private List<ChannelItemInfo> BuildSourceList()
    {
        return GetEnabledSources().Select(source => new ChannelItemInfo
        {
            Name = source.Name,
            Id = $"src_{source.Id}",
            Type = ChannelItemType.Folder,
            MediaType = ChannelMediaType.Video,
            ImageUrl = null
        }).ToList();
    }

    private async Task<List<ChannelItemInfo>> GetSourceItemsAsync(string folderId, InternalChannelItemQuery query, CancellationToken ct)
    {
        // folderId examples:
        //   src_{sourceId}           -> sub-source list (or root content if no sub-sources)
        //   src_{sourceId}/sub_{idx} -> latest + categories for this sub-source
        //   src_{sourceId}/sub_{idx}/latest   -> latest videos
        //   src_{sourceId}/sub_{idx}/cat_{typeId} -> category videos

        var parts = folderId.Split('/');
        var sourceId = parts[0].Substring(4); // strip "src_"
        var sources = GetEnabledSources();
        var source = sources.FirstOrDefault(s => s.Id == sourceId);
        if (source == null) return [];

        var subSources = SyncService.GetCachedSubSources(sourceId);

        // Level 1: src_{sourceId} -> show sub-source list (or content root if no sub-sources)
        if (parts.Length == 1)
        {
            if (subSources.Count > 0)
            {
                return BuildSubSourceList(sourceId, subSources);
            }
            else
            {
                // No sub-sources: trigger sync and show source root
                await EnsureSourceSynced(source, ct);
                return BuildSourceRoot(sourceId, "0");
            }
        }

        // Level 2: src_{sourceId}/sub_{idx}
        if (parts.Length == 2 && parts[1].StartsWith("sub_"))
        {
            var subIdx = parts[1].Substring(4);
            await EnsureSubSourceSynced(source, subIdx, ct);
            return BuildSourceRoot(sourceId, subIdx);
        }

        // Level 3: src_{sourceId}/sub_{idx}/latest or cat_{typeId}
        if (parts.Length == 3 && parts[1].StartsWith("sub_"))
        {
            var subIdx = parts[1].Substring(4);
            var items = SyncService.GetCachedItems(sourceId, subIdx);
            var action = parts[2];

            if (action == "latest")
                return BuildLatestItems(items, query);

            if (action.StartsWith("cat_"))
                return BuildCategoryItems(items, query, action);

            return BuildSourceRoot(sourceId, subIdx);
        }

        return [];
    }

    private async Task EnsureSourceSynced(ContentSource source, CancellationToken ct)
    {
        var items = SyncService.GetCachedItems(source.Id, "0");
        if (items.Count == 0)
        {
            await SyncService.SyncSourceAsync(source, ct);
        }
    }

    private async Task EnsureSubSourceSynced(ContentSource source, string subIdx, CancellationToken ct)
    {
        var items = SyncService.GetCachedItems(source.Id, subIdx);
        if (items.Count == 0)
        {
            var subSources = SyncService.GetCachedSubSources(source.Id);
            var idx = int.TryParse(subIdx, out var i) ? i : -1;
            if (idx >= 0 && idx < subSources.Count)
            {
                await SyncService.SyncSourceAsync(source, ct);
            }
        }
    }

    private List<ChannelItemInfo> BuildSubSourceList(string sourceId, List<CmsSubSource> subSources)
    {
        return subSources.Select((sub, idx) => new ChannelItemInfo
        {
            Name = sub.Name,
            Id = $"src_{sourceId}/sub_{idx}",
            Type = ChannelItemType.Folder,
            MediaType = ChannelMediaType.Video,
            ImageUrl = null
        }).ToList();
    }

    private List<ChannelItemInfo> BuildSourceRoot(string sourceId, string subIdx)
    {
        var results = new List<ChannelItemInfo>
        {
            new()
            {
                Name = "最新",
                Id = $"src_{sourceId}/sub_{subIdx}/latest",
                Type = ChannelItemType.Folder,
                MediaType = ChannelMediaType.Video,
                ImageUrl = null
            }
        };

        var items = SyncService.GetCachedItems(sourceId, subIdx);
        var categories = items.GroupBy(x => (x.Type, x.TypeName))
            .Where(g => !string.IsNullOrEmpty(g.Key.TypeName))
            .OrderBy(g => g.Key.Type);

        foreach (var cat in categories)
        {
            results.Add(new ChannelItemInfo
            {
                Name = cat.Key.TypeName,
                Id = $"src_{sourceId}/sub_{subIdx}/cat_{cat.Key.Type}",
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

    public bool IsEnabledFor(string userId) => true;
}