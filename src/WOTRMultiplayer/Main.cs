using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityModManagerNet;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Config.UnityMod;
using WOTRMultiplayer.DI;

namespace WOTRMultiplayer
{
    // SettingsRoot.Game.TurnBased.EnableTurnBasedMode
    // AutoPauseController

    // EventBus -> make sure INetworkEventSub
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

        public static bool Load(UnityModManager.ModEntry entry)
        {
            try
            {
                _settings = UnityModManager.ModSettings.Load<UnityModManagerSettings>(entry);
                _serviceProvider = DIFactory.Create(_settings);
                _logger = _serviceProvider.GetService<ILogger<Main>>();
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load mod");
                throw;
            }

            return true;
        }

        public static void InitializePortraits()
        {
            _logger.LogInformation("Initializing portrait sprites");
            _serviceProvider.GetService<IPortraitProvider>().Initialize();
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
            _settings.UseDebugConsole = UnityEngine.GUILayout.Toggle(_settings.UseDebugConsole, $"Use Debug Console (requires restart)", UnityEngine.GUILayout.ExpandWidth(false));
            UnityEngine.GUILayout.EndHorizontal();
        }
    }
}
