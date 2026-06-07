using LibreMedia.Channels;
using LibreMedia.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LibreMedia;

public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<CmsApiClient>();
        serviceCollection.AddSingleton<ContentSyncService>();
        serviceCollection.AddSingleton<IChannel, LibreChannel>();
    }
}
