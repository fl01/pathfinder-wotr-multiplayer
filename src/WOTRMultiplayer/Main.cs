using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Kingmaker.PubSubSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityModManagerNet;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.PubSub;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Config.UnityMod;
using WOTRMultiplayer.DI;

namespace WOTRMultiplayer
{
    // SettingsRoot.Game.TurnBased.EnableTurnBasedMode
    // party skills checks enabled
    // same speed in combat
    // loot bodies in combat = false
    public class Main
    {
        private static UnityModManagerSettings _settings;
        private static IServiceProvider _serviceProvider;
        private static ILogger<Main> _logger;

        public static IMultiplayer Multiplayer { get; private set; }

        public const int MaxCharacters = 6;

        public static ILogger<T> GetLogger<T>()
        {
            return _serviceProvider.GetService<ILogger<T>>();
        }

        public static bool AddUnitIdToOvertip => _settings.AddUnitIdToOvertip;

        public static bool Load(UnityModManager.ModEntry entry)
        {
            try
            {
                _settings = UnityModManager.ModSettings.Load<UnityModManagerSettings>(entry);
                _serviceProvider = DIFactory.Create(_settings);
                _logger = _serviceProvider.GetService<ILogger<Main>>();
                Subscribe();
            }
            catch (Exception ex)
            {
                // somethign when wrong and our logger is not available here
                entry.Logger.Error($"Unable to initialize mod. Error={ex}");
                throw;
            }

            _logger.LogInformation("Loading mod");

            try
            {
                Multiplayer = _serviceProvider.GetService<IMultiplayer>();

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
            _logger.LogError(e.Exception, "Unhandled task exception");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.LogError(e.ExceptionObject as Exception, "Unhandled exception");
        }

        public static void InitializePortraits()
        {
            _logger.LogInformation("Initializing portrait sprites");
            _serviceProvider.GetService<IResourceProvider>().Initialize();
        }

        private static void Subscribe()
        {
            var globalMultiplayerSubscriber = _serviceProvider.GetService<IGlobalMultiplayerSubscriber>();
            EventBus.Subscribe(globalMultiplayerSubscriber);

            var globalMultiplayerUnitCommandSubscriber = _serviceProvider.GetService<IGlobalMultiplayerUnitCommandSubscriber>();
            EventBus.Subscribe(globalMultiplayerUnitCommandSubscriber);
        }

        private static bool OnUnload(UnityModManager.ModEntry entry)
        {
            _logger.LogInformation("Unloading on the fly is not supported. Please restart the game");
            return true;
        }

        private static void OnSaveGui(UnityModManager.ModEntry entry)
        {
            _settings.Save(entry);
        }

        private static void OnGui(UnityModManager.ModEntry entry)
        {
            UnityEngine.GUILayout.BeginHorizontal();
            _settings.UseDebugConsole = UnityEngine.GUILayout.Toggle(_settings.UseDebugConsole, $"Use Debug Console (requires game client restart)", UnityEngine.GUILayout.ExpandWidth(false));
            UnityEngine.GUILayout.EndHorizontal();
            UnityEngine.GUILayout.BeginHorizontal();
            _settings.AddUnitIdToOvertip = UnityEngine.GUILayout.Toggle(_settings.AddUnitIdToOvertip, $"Add UnitId to overtip (requires area reload)", UnityEngine.GUILayout.ExpandWidth(false));
            UnityEngine.GUILayout.EndHorizontal();
        }
    }
}
