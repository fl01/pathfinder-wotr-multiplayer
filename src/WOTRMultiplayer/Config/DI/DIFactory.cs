using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Localization;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Config.Mapping;
using WOTRMultiplayer.Networking.Extensions;
using WOTRMultiplayer.PubSub;
using WOTRMultiplayer.Services;
using WOTRMultiplayer.Services.GameInteraction;
using WOTRMultiplayer.Services.Hashing;
using WOTRMultiplayer.Services.IO;
using WOTRMultiplayer.Services.Localization;
using WOTRMultiplayer.Services.Random;
using WOTRMultiplayer.Services.Settings;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UI.Controllers;

namespace WOTRMultiplayer.Config.DI
{
    public static class DIFactory
    {
        public static IServiceProvider Create(UnityModManagerSettings settings)
        {
            var serviceCollection = new ServiceCollection();
            var consoleLogLevel = (LogEventLevel)settings.ConsoleMinimumLogLevel;
            var fileLogLevel = (LogEventLevel)settings.FileMinimumLogLevel;

            Log.Logger = Logging.LoggerFactory.Create(settings.UseDebugConsole, consoleLogLevel, fileLogLevel);

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
            serviceCollection.AddSingleton<IValueGenerator, PredictableValueGenerator>();

            serviceCollection.AddSingleton<IFileSystemService, FileSystemService>();
            serviceCollection.AddSingleton<IResourceProvider, ResourceBundleProvider>();
            serviceCollection.AddSingleton<IUIFactory, UIFactory>();
            serviceCollection.AddSingleton<IHashService, HashService>();
            serviceCollection.AddSingleton<IDiceRollStorage, ClaimableDiceRollStorage>();
            serviceCollection.AddSingleton<ILocalizationService, LocalizationService>();
            serviceCollection.AddSingleton<ILocalizationManagerAccessor, LocalizationManagerAccessor>();

            serviceCollection.AddSingleton<ILobbyWindowController, LobbyWindowController>();
            serviceCollection.AddSingleton<IHostMenuItemController, HostMenuItemController>();
            serviceCollection.AddSingleton<IJoinMenuItemController, JoinMenuItemController>();

            serviceCollection.AddSingleton<IGameInteractionService, GameInteractionService>();
            serviceCollection.AddSingleton<IEquipmentDefinitions, EquipmentDefinitions>();

            serviceCollection.AddSingleton<MultiplayerSubscriber>();
            serviceCollection.AddSingleton<MultiplayerUnitEquipmentSubscriber>();
            serviceCollection.AddSingleton<MultiplayerCampingStateSubscriber>();
            serviceCollection.AddSingleton<MultiplayerCombatSubscriber>();

            serviceCollection.AddSingleton<IMultiplayerActorAccessor, MultiplayerActorAccessor>();
            serviceCollection.AddSingleton<IMultiplayerRollsProcessor, MultiplayerRollProcessor>();
            serviceCollection.AddSingleton<IMultiplayer, Multiplayer>();
            serviceCollection.AddSingleton<IMultiplayerHost, MultiplayerHost>();
            serviceCollection.AddSingleton<IMultiplayerClient, MultiplayerClient>();
            serviceCollection.AddSingleton<IMultiplayerSettingsService, MultiplayerSettingsProvider>();
            serviceCollection.AddSingleton<ISettingsControllerAccessor, SettingsControllerAccessor>();

            serviceCollection.ConfigureNetworking();

            return serviceCollection.BuildServiceProvider();
        }
    }
}
