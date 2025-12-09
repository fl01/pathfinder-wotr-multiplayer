using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
        public static bool Game_StartMode_Prefix(GameModeType type)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var allowedToRun = Main.Multiplayer.OnStartGameMode(type);
            return allowedToRun;
        }

        [HarmonyPatch(typeof(Game), nameof(Game.StopMode))]
        [HarmonyPrefix]
        public static bool Game_StopMode_Prefix(GameModeType type)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var allowedToRun = Main.Multiplayer.OnStopGameMode(type);
            return allowedToRun;
        }

        [HarmonyPatch(typeof(Game), nameof(Game.PauseBind))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Game_PauseBind_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(GameInstancePatches), nameof(GameInstancePatches.TogglePause));
            var lookFor = AccessTools.PropertyGetter(typeof(Game), nameof(Game.IsPaused));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GameInstancePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-2).RemoveInstructions(6);
            var newInstructions = new List<CodeInstruction>()
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<GameInstancePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void TogglePause(Game game)
        {
            var isPaused = game.IsPaused;
            var canTogglePause = Main.Multiplayer.TogglePause(isPaused);
            if (!Main.Multiplayer.IsActive || canTogglePause)
            {
                game.IsPaused = !game.IsPaused;
            }
        }
    }
}
