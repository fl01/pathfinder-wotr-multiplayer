using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.MP.Actors;
using WOTRMultiplayer.Abstractions.PubSub;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Config.Mapping;
using WOTRMultiplayer.GameInteraction;
using WOTRMultiplayer.Hashing;
using WOTRMultiplayer.IO;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Actors;
using WOTRMultiplayer.Networking.Extensions;
using WOTRMultiplayer.PubSub;
using WOTRMultiplayer.Random;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UI.Controllers;
using WOTRMultiplayer.Unity;

namespace WOTRMultiplayer.DI
{
    public static class DIFactory
    {
        public static IServiceProvider Create(Config.UnityMod.UnityModManagerSettings settings)
        {
            var serviceCollection = new ServiceCollection();
            if (!Enum.TryParse<LogEventLevel>(settings.MinimumLogLevel, true, out var logLevel))
            {
                logLevel = LogEventLevel.Information;
            }

            Log.Logger = Logging.LoggerFactory.Create(settings.UseDebugConsole, logLevel);

            serviceCollection.AddLogging(x =>
            {
                x.ClearProviders();
                x.AddSerilog(Log.Logger);
            });

            serviceCollection.AddAutoMapper(cfg =>
            {
                cfg.AddProfile<NetworkMessagesProfile>();
            });

            serviceCollection.AddSingleton<IMainThreadAccessor, MainThreadAccessor>();
            serviceCollection.AddSingleton<IUniqueIdGenerator, PredictableUniqueIdGenerator>();

            serviceCollection.AddSingleton<IFileSystemService, FileSystemService>();
            serviceCollection.AddSingleton<IResourceProvider, ResourceBundleProvider>();
            serviceCollection.AddSingleton<IUIFactory, UIFactory>();
            serviceCollection.AddSingleton<IHashService, HashService>();
            serviceCollection.AddSingleton<IDiceRollStorage, ClaimableDiceRollStorage>();

            serviceCollection.AddSingleton<ILobbyWindowController, LobbyWindowController>();
            serviceCollection.AddSingleton<IHostMenuItemController, HostMenuItemController>();
            serviceCollection.AddSingleton<IJoinMenuItemController, JoinMenuItemController>();

            serviceCollection.AddSingleton<IGameInteractionService, GameInteractionService>();
            serviceCollection.AddSingleton<IEquipmentDefinitions, EquipmentDefinitions>();
            serviceCollection.AddSingleton<IGlobalMultiplayerSubscriber, GlobalMultiplayerSubscriber>();
            serviceCollection.AddSingleton<IGlobalMultiplayerUnitCommandSubscriber, GlobalMultiplayerUnitSubscriber>();

            serviceCollection.AddSingleton<IMultiplayerActorAccessor, MultiplayerActorAccessor>();
            serviceCollection.AddSingleton<IMultiplayerRollsProcessor, MultiplayerRollProcessor>();
            serviceCollection.AddSingleton<IMultiplayer, Multiplayer>();
            serviceCollection.AddSingleton<IMultiplayerHost, MultiplayerHost>();
            serviceCollection.AddSingleton<IMultiplayerClient, MultiplayerClient>();
            serviceCollection.AddSingleton<IMultiplayerSettingsProvider, MultiplayerSettingsProvider>();

            serviceCollection.ConfigureNetworking();

            return serviceCollection.BuildServiceProvider();
        }
    }
}
