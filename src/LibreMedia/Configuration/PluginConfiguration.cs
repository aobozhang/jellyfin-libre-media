using LibreMedia.Models;
using MediaBrowser.Model.Plugins;

namespace LibreMedia.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        ContentSources = [];
        SyncIntervalMinutes = 60;
        EnableAutoSync = true;
    }

    public List<ContentSource> ContentSources { get; set; }

    public int SyncIntervalMinutes { get; set; }

    public bool EnableAutoSync { get; set; }
}