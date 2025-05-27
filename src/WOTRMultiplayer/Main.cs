using System.Reflection;
using HarmonyLib;
using Serilog;
using UnityModManagerNet;
using WOTRMultiplayer.Config.UnityMod;
using WOTRMultiplayer.Logging;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer
{
    public class Main
    {
        private static UnityModManagerSettings _settings;

        public static Multiplayer Multiplayer { get; private set; }

        private static ILogger _logger;

        public static bool Load(UnityModManager.ModEntry entry)
        {
            _settings = UnityModManager.ModSettings.Load<UnityModManagerSettings>(entry);
            _logger = Log.Logger = LoggerFactory.Create(_settings.UseDebugConsole);

            _logger.Information("Loading mod");

            var host = new MultiplayerHost(new Networking.NetworkServer());
            var client = new MultiplayerClient(new Networking.NetworkServerClient());
            Multiplayer = new Multiplayer(new UIFactory(), host, client);

            entry.OnGUI += OnGui;
            entry.OnSaveGUI += OnSaveGui;
            entry.OnUnload += OnUnload;

            try
            {
                var harmony = new Harmony(entry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Harmony patching has failed");
                throw;
            }

            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry entry)
        {
            _logger.Information("Unloading");
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
