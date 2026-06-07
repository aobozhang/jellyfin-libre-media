using System.Globalization;
using LibreMedia.Channels;
using LibreMedia.Configuration;
using LibreMedia.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace LibreMedia;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IPluginServiceRegistrator
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "LibreMedia";

    public override Guid Id => Guid.Parse("c3d4e5f6-7890-abcd-ef12-345678901234");

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }

    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<CmsApiClient>();
        serviceCollection.AddSingleton<ContentSyncService>();
        serviceCollection.AddSingleton<IChannel, LibreChannel>();
    }
}
