using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using HarmonyLib;
using Kingmaker.PubSubSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using UnityEngine;
using UnityModManagerNet;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.Localization;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Config.DI;
using WOTRMultiplayer.Localization;
using WOTRMultiplayer.Services.PubSub;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer
{
    public class Main
    {
        private static ILogger<Main> _logger;

        public static IServiceProvider ServiceProvider { get; private set; }

        public static UnityModManagerSettings ModManagerSettings { get; private set; }

        public static ILobbyWindowController Lobby { get; private set; }

        public static IMultiplayer Multiplayer { get; private set; }

        public static IPlayerNotificationService PlayerNotification { get; private set; }

        public static IGameStateLookupService State { get; private set; }

        public static IUIAccessor UIAccessor { get; private set; }

        public static IMapper Mapper { get; private set; }

        public static IHashService HashService { get; private set; }

        public static IMultiplayerRollsProcessor Rolls { get; private set; }

        public const int MaxCharactersInParty = 6;

        public static ILogger<T> GetLogger<T>()
        {
            return ServiceProvider.GetService<ILogger<T>>();
        }

        public static bool Load(UnityModManager.ModEntry entry)
        {
            try
            {
                ModManagerSettings = UnityModManager.ModSettings.Load<UnityModManagerSettings>(entry);
                ModManagerSettings.ModFolder = entry.Path;
                ServiceProvider = DIFactory.Create(ModManagerSettings, ModManagerSettings.ModFolder);

                _logger = ServiceProvider.GetService<ILogger<Main>>();
            }
            catch (Exception ex)
            {
                entry.Logger.Error($"Failed to initialize. Error={ex}");
                throw;
            }

            _logger.LogInformation("Loading mod. ConsoleLogLevel={ConsoleLogLevel}, FileLogLevel={FileLogLevel}", (LogEventLevel)ModManagerSettings.ConsoleMinimumLogLevel, (LogEventLevel)ModManagerSettings.FileMinimumLogLevel);

            try
            {
                Subscribe();
                WellKnownKeysInitializer.Run();
                WellKnownSettings.Initialize();

                Multiplayer = ServiceProvider.GetRequiredService<IMultiplayer>();
                Rolls = ServiceProvider.GetRequiredService<IMultiplayerRollsProcessor>();
                UIAccessor = ServiceProvider.GetRequiredService<IUIAccessor>();
                Mapper = ServiceProvider.GetRequiredService<IMapper>();
                PlayerNotification = ServiceProvider.GetRequiredService<IPlayerNotificationService>();
                State = ServiceProvider.GetRequiredService<IGameStateLookupService>();
                HashService = ServiceProvider.GetRequiredService<IHashService>();
                Lobby = ServiceProvider.GetRequiredService<ILobbyWindowController>();

                entry.OnGUI += OnGui;
                entry.OnSaveGUI += OnSaveGui;
                entry.OnToggle += OnToggle;

                var harmony = new Harmony(entry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                var patchedMethods = harmony.GetPatchedMethods().Count();
                _logger.LogInformation("Harmony patching has been finished. PatchedMethods={PatchedMethods}", patchedMethods);
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load mod");
                throw;
            }

            return true;
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger.LogError(e.Exception, "Unobserved task exception");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.LogError(e.ExceptionObject as Exception, "Unhandled exception");
        }

        public static void Initialize()
        {
            InitializePortraits();
            InitializeMultiplayerSettings();
        }

        public static void UpdateLocale(string locale)
        {
            ServiceProvider.GetService<ILocalizationService>().UpdateLocale(locale);
        }

        private static void InitializePortraits()
        {
            _logger.LogInformation("Initializing portrait sprites");
            ServiceProvider.GetService<IResourceProvider>().Initialize();
        }

        private static void InitializeMultiplayerSettings()
        {
            _logger.LogInformation("Initializing multiplayer settings");
            ServiceProvider.GetService<IMultiplayerSettingsService>().Initialize();
        }

        private static void Subscribe()
        {
            var genericSubscriber = ServiceProvider.GetService<MultiplayerSubscriber>();
            EventBus.Subscribe(genericSubscriber);

            var unitEquipmentSubscriber = ServiceProvider.GetService<MultiplayerUnitEquipmentSubscriber>();
            EventBus.Subscribe(unitEquipmentSubscriber);

            var campingStateSubscriber = ServiceProvider.GetService<MultiplayerCampingStateSubscriber>();
            EventBus.Subscribe(campingStateSubscriber);

            var combatSubscriber = ServiceProvider.GetService<MultiplayerCombatSubscriber>();
            EventBus.Subscribe(combatSubscriber);

            var kingdomSubscriber = ServiceProvider.GetService<MultiplayerKingdomSubscriber>();
            EventBus.Subscribe(kingdomSubscriber);
        }

        private static bool OnToggle(UnityModManager.ModEntry entry, bool isOn)
        {
            if (!isOn)
            {
                _logger.LogWarning("Disabling on the fly is not supported. Please restart the game");
            }

            return true;
        }

        private static void OnSaveGui(UnityModManager.ModEntry entry)
        {
            ModManagerSettings.Save(entry);
        }

        private static void OnGui(UnityModManager.ModEntry entry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("---Debug Console settings (requires game client restart)");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ModManagerSettings.UseDebugConsole = GUILayout.Toggle(ModManagerSettings.UseDebugConsole, $"Enable Debug Console", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Minimum Console Log Level (Information is recommended, Debug - if you are mad)");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ModManagerSettings.ConsoleMinimumLogLevel = GUILayout.Toolbar(ModManagerSettings.ConsoleMinimumLogLevel, Enum.GetNames(typeof(LogEventLevel)));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Minimum File Log Level (Debug is recommended for maximum info)");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ModManagerSettings.FileMinimumLogLevel = GUILayout.Toolbar(ModManagerSettings.FileMinimumLogLevel, Enum.GetNames(typeof(LogEventLevel)));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("---Utils");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ModManagerSettings.AddUnitIdToOvertip = GUILayout.Toggle(ModManagerSettings.AddUnitIdToOvertip, $"Add UnitId to overtip (requires area reload)", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }
    }
}
