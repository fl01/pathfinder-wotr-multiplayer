using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Kingmaker.PubSubSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using UnityEngine;
using UnityModManagerNet;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Localization;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Config.DI;
using WOTRMultiplayer.Localization;
using WOTRMultiplayer.Services.PubSub;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer
{
    public class Main
    {
        private static IServiceProvider _serviceProvider;
        private static ILogger<Main> _logger;

        public static UnityModManagerSettings ModManagerSettings { get; private set; }

        public static IMultiplayer Multiplayer { get; private set; }

        public static IUIAccessor UIAccessor { get; private set; }

        public static IMultiplayerRollsProcessor Rolls { get; private set; }

        public const int MaxCharactersInParty = 6;

        public static ILogger<T> GetLogger<T>()
        {
            return _serviceProvider.GetService<ILogger<T>>();
        }

        public static bool Load(UnityModManager.ModEntry entry)
        {
            try
            {
                ModManagerSettings = UnityModManager.ModSettings.Load<UnityModManagerSettings>(entry);
                _serviceProvider = DIFactory.Create(ModManagerSettings);

                _logger = _serviceProvider.GetService<ILogger<Main>>();
            }
            catch (Exception ex)
            {
                entry.Logger.Error($"Failed to initialize. Error={ex}");
                throw;
            }

            _logger.LogInformation("Loading mod");

            try
            {
                Subscribe();

                WellKnownKeysInitializer.Run();
                WellKnownSettings.Initialize();

                Multiplayer = _serviceProvider.GetRequiredService<IMultiplayer>();
                Rolls = _serviceProvider.GetRequiredService<IMultiplayerRollsProcessor>();
                UIAccessor = _serviceProvider.GetRequiredService<IUIAccessor>();

                entry.OnGUI += OnGui;
                entry.OnSaveGUI += OnSaveGui;
                entry.OnUnload += OnUnload;

                _logger.LogInformation("harmony patching");

                var harmony = new Harmony(entry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

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
            _serviceProvider.GetService<ILocalizationService>().UpdateLocale(locale);
        }

        private static void InitializePortraits()
        {
            _logger.LogInformation("Initializing portrait sprites");
            _serviceProvider.GetService<IResourceProvider>().Initialize();
        }

        private static void InitializeMultiplayerSettings()
        {
            _logger.LogInformation("Initializing multiplayer settings");
            _serviceProvider.GetService<IMultiplayerSettingsService>().Initialize();
        }


        private static void Subscribe()
        {
            var genericSubscriber = _serviceProvider.GetService<MultiplayerSubscriber>();
            EventBus.Subscribe(genericSubscriber);

            var unitEquipmentSubscriber = _serviceProvider.GetService<MultiplayerUnitEquipmentSubscriber>();
            EventBus.Subscribe(unitEquipmentSubscriber);

            var campingStateSubscriber = _serviceProvider.GetService<MultiplayerCampingStateSubscriber>();
            EventBus.Subscribe(campingStateSubscriber);

            var combatSubscriber = _serviceProvider.GetService<MultiplayerCombatSubscriber>();
            EventBus.Subscribe(combatSubscriber);
        }

        private static bool OnUnload(UnityModManager.ModEntry entry)
        {
            _logger.LogInformation("Unloading on the fly is not supported. Please restart the game");
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
