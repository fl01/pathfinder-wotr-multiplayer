using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.GameInstance
{
    [HarmonyPatch]
    public class GameInstancePatches
    {
        [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
        [HarmonyPrefix]
        public static void Game_LoadGame_Postfix(SaveInfo saveInfo)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (Game.Instance.Player.MainCharacter == null)
            {
                Main.GetLogger<GameInstancePatches>().LogInformation("Force load hook is skipped since player is not in the game");
                return;
            }

            Main.Multiplayer.ForceLoadGame(saveInfo);
        }

        [HarmonyPatch(typeof(Game), nameof(Game.StartMode))]
        [HarmonyPrefix]
        public static bool Game_StartMode_Prefix(Game __instance, GameModeType type)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var allowedToRun = Main.Multiplayer.StartGameMode(type);
            return allowedToRun;
        }

        [HarmonyPatch(typeof(Game), nameof(Game.StopMode))]
        [HarmonyPrefix]
        public static bool Game_StopMode_Prefix(Game __instance, GameModeType type)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var allowedToRun = Main.Multiplayer.StopGameMode(type);
            return allowedToRun;
        }
    }
}
