using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.UI.MVVM._PCView.GlobalMap;
using Kingmaker.UI.MVVM._PCView.InGame;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.GameInstance
{
    [HarmonyPatch]
    public class GamePatches
    {
        [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
        [HarmonyPrefix]
        public static void Game_LoadGame_Prefix(SaveInfo saveInfo)
        {
            if (!Main.Multiplayer.IsActive || !Game.Instance.Player.IsInGame)
            {
                return;
            }

            Main.Multiplayer.ForceLoadGame(saveInfo.GameId, saveInfo.FolderName);
        }

        [HarmonyPatch(typeof(Game), nameof(Game.StartMode))]
        [HarmonyPrefix]
        public static bool Game_StartMode_Prefix(GameModeType type)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            if (type == GameModeType.FullScreenUi)
            {
                FixFullScreenUiToggle(true);
                return false;
            }
            else if (type == GameModeType.EscMode)
            {
                return false;
            }

            Main.Multiplayer.OnStartGameMode(type);
            return true;
        }

        [HarmonyPatch(typeof(Game), nameof(Game.StopMode))]
        [HarmonyPrefix]
        public static bool Game_StopMode_Prefix(GameModeType type)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            if (type == GameModeType.FullScreenUi)
            {
                FixFullScreenUiToggle(false);
                return false;
            }
            else if (type == GameModeType.EscMode)
            {
                return false;
            }

            Main.Multiplayer.OnStopGameMode(type);
            return true;
        }

        /// <summary>
        /// EscMode and FullScreenUi game modes are never actually started. We block them to prevent the game from 'fake' pausing (reducing timeScale to 0, stopping moving units, etc.) in multiplayer - the game should keep running when a player opens Esc menu, settings, inventory, etc.
        /// This, however, causes some undesirable side effects. For example, opened inventory (fullscreenui) overlaps with the combat log. Even though the combat log is not visible, it still captures all mouse inputs, so you can't use some inventory slots or interact with Finnean.
        /// An alternative to blocking these modes would be to stop controllers from being deactivated (starting FullScreenUi mode stops Default mode, which calls Deactivate on every controller),
        /// but that looks like a rabbit hole with an unclear amount of work to fix every controller that needs to stay active.
        /// For now, fixing the side effects feels like the safer and more reasonable approach
        /// <param name="isStart"></param>
        private static void FixFullScreenUiToggle(bool isStart)
        {
            var combatLogView = (Game.Instance.RootUiContext.m_UIView as InGamePCView)?.m_StaticPartPCView?.m_CombatLogPCView ?? (Game.Instance.RootUiContext.m_UIView as GlobalMapPCView)?.m_CombatLogPCView;
            if (combatLogView == null)
            {
                Main.GetLogger<GamePatches>().LogError("Unable to fix full screen game mode sideeffects due to missing combat log view");
                return;
            }

            if (isStart)
            {
                combatLogView.OnGameModeStart(GameModeType.FullScreenUi);
                return;
            }

            combatLogView.OnGameModeStop(GameModeType.FullScreenUi);
        }

        [HarmonyPatch(typeof(Game), nameof(Game.PauseBind))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Game_PauseBind_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(GamePatches), nameof(GamePatches.TogglePause));
            var lookFor = AccessTools.PropertyGetter(typeof(Game), nameof(Game.IsPaused));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GamePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-2).RemoveInstructions(6);
            var newInstructions = new List<CodeInstruction>()
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<GamePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
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
