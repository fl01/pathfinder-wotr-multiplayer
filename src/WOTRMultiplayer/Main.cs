using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityModManagerNet;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Config.UnityMod;
using WOTRMultiplayer.DI;

namespace WOTRMultiplayer
{
    public class Main
    {
        private static UnityModManagerSettings _settings;
        private static IServiceProvider _serviceProvider;

        public static IMultiplayer Multiplayer { get; private set; }

        private static ILogger<Main> _logger;

        public static bool Load(UnityModManager.ModEntry entry)
        {
            _settings = UnityModManager.ModSettings.Load<UnityModManagerSettings>(entry);
            _serviceProvider = DIFactory.Create(_settings);
            _logger = _serviceProvider.GetService<ILogger<Main>>();
            _logger.LogInformation("Loading mod");
            try
            {
                Multiplayer = _serviceProvider.GetService<IMultiplayer>();

                entry.OnGUI += OnGui;
                entry.OnSaveGUI += OnSaveGui;
                entry.OnUnload += OnUnload;


                var harmony = new Harmony(entry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to load mod");
                throw;
            }

            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry entry)
        {
            _logger.LogInformation("Unloading");
            Multiplayer.Dispose();
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
