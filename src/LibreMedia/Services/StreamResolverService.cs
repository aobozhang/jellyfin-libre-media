using LibreMedia.Models;

namespace LibreMedia.Services;

public class StreamResolverService
{
    private static readonly HashSet<string> PlayableExtensions = [".m3u8", ".mp4", ".webm", ".mkv", ".avi"];

    public string ResolveStreamUrl(CmsVideoItem item)
    {
        if (string.IsNullOrEmpty(item.PlayUrl))
            return string.Empty;

        // If it looks like a direct playable URL, return as-is
        if (IsPlayableUrl(item.PlayUrl))
            return item.PlayUrl;

        // Otherwise proxy through our controller
        return $"/LibreMedia/Stream/{item.Id}?url={Uri.EscapeDataString(item.PlayUrl)}";
    }

    public static bool IsPlayableUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        var lower = url.ToLowerInvariant();
        return lower.StartsWith("http") && PlayableExtensions.Any(ext => lower.Contains(ext));
    }
}