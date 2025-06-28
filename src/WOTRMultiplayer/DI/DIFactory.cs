using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.PubSub;
using WOTRMultiplayer.Abstractions.Saves;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.GameInteraction;
using WOTRMultiplayer.IO;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.Networking.Extensions;
using WOTRMultiplayer.PubSub;
using WOTRMultiplayer.Saves;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UI.Lobby;
using WOTRMultiplayer.UI.Menu.Items;
using WOTRMultiplayer.Unity;

namespace WOTRMultiplayer.DI
{
    public static class DIFactory
    {
        public static IServiceProvider Create(Config.UnityMod.UnityModManagerSettings settings)
        {
            var serviceCollection = new ServiceCollection();
            Log.Logger = Logging.LoggerFactory.Create(settings.UseDebugConsole);

            serviceCollection.AddLogging(x =>
            {
                x.ClearProviders();
                x.AddSerilog(Log.Logger);
            });

            serviceCollection.AddSingleton<IMainThreadAccessor, MainThreadAccessor>();
            serviceCollection.AddSingleton<ISaveGameService, SaveGameService>();

            serviceCollection.AddSingleton<IFileSystemService, FileSystemService>();
            serviceCollection.AddSingleton<IPortraitProvider, ResourceLibraryPortraitProvider>();
            serviceCollection.AddSingleton<IUIFactory, UIFactory>();
            serviceCollection.AddSingleton<IUIFactory, UIFactory>();

            serviceCollection.AddSingleton<ILobbyWindowController, LobbyWindowController>();
            serviceCollection.AddSingleton<IHostMenuItemController, HostMenuItemController>();
            serviceCollection.AddSingleton<IJoinMenuItemController, JoinMenuItemController>();

            serviceCollection.AddSingleton<IGameInteractionService, GameInteractionService>();
            serviceCollection.AddSingleton<IGlobalMultiplayerSubscriber, GlobalSubscriber>();

            serviceCollection.AddSingleton<IMultiplayer, Multiplayer>();
            serviceCollection.AddSingleton<IMultiplayerHost, MultiplayerHost>();
            serviceCollection.AddSingleton<IMultiplayerClient, MultiplayerClient>();
            serviceCollection.AddSingleton<IMultiplayerSettingsProvider, MultiplayerSettingsProvider>();

            serviceCollection.ConfigureNetworking();

            return serviceCollection.BuildServiceProvider();
        }
    }
}
