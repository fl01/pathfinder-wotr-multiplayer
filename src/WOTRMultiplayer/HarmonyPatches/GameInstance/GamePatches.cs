using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Area;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.Globalmap;
using Kingmaker.UI.Common;
using Kingmaker.UI.FullScreenUITypes;
using Kingmaker.UI.MVVM;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;

namespace WOTRMultiplayer.HarmonyPatches.GameInstance
{
    [HarmonyPatch]
    public class GamePatches
    {
        [HarmonyPatch(typeof(Game), nameof(Game.LoadArea), [typeof(BlueprintArea), typeof(BlueprintAreaEnterPoint), typeof(AutoSaveMode), typeof(bool), typeof(SaveInfo), typeof(Action)])]
        [HarmonyPrefix]
        public static void Game_LoadArea_Prefix(ref Action callback)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (callback == null)
            {
                callback = Main.Multiplayer.OnAreaLoaded;
            }
            else
            {
                callback += Main.Multiplayer.OnAreaLoaded;
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
        [HarmonyPrefix]
        public static void Game_LoadGame_Prefix(SaveInfo saveInfo)
        {
            if (!Main.Multiplayer.IsActive || !Game.Instance.Player.IsInGame || RootUIContext.Instance.InGameVM?.StaticPartVM?.CharGenContextVM?.CharGenVM?.Value != null || !saveInfo.CheckDlcAvailable())
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

        [HarmonyPatch(typeof(Game), nameof(Game.DoStopMode))]
        [HarmonyPostfix]
        public static void Game_DoStopMode_Postfix(GameModeType type)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            // expected 'Start FullScreen -> Stop Dialog -> DoStop Dialog -> DoStart FullScreen' sequence is broken because fullscreen mode is always denied
            // 'DoStart FullScreen' action is never executed, so combat log is never hidden in case of Dialog->Vendor option
            if (type == GameModeType.Dialog && Game.Instance.Vendor.IsTrading)
            {
                FixFullScreenUiToggle(true);
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.PauseBind))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Game_PauseBind_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());

            var lookForGlobalMap = AccessTools.Method(typeof(UIUtility), nameof(UIUtility.IsGlobalMap));
            var breakGlobalMapCall = AccessTools.Method(typeof(GamePatches), nameof(GamePatches.BreakGlobalMap));
            var matcher = new CodeMatcher(instructions, generator);
            matcher.End().CreateLabel(out var endLabel);
            var match = matcher.Start().SearchForward(x => x.Calls(lookForGlobalMap));
            if (match.IsInvalid)
            {
                Main.GetLogger<GamePatches>().LogError("Transpiler has not been applied (BreakGlobalMap). Target={Target}", target);
                return instructions;
            }

            var globalMapInstructions = new List<CodeInstruction>()
            {
                new (OpCodes.Call, breakGlobalMapCall),
                new (OpCodes.Brfalse_S, endLabel),
            };
            match = match.Advance(2).Insert(globalMapInstructions);

            var pauseToggleCall = AccessTools.Method(typeof(GamePatches), nameof(GamePatches.TogglePause));
            var lookForPause = AccessTools.PropertyGetter(typeof(Game), nameof(Game.IsPaused));
            match = matcher.SearchForward(x => x.Calls(lookForPause));
            if (match.IsInvalid)
            {
                Main.GetLogger<GamePatches>().LogError("Transpiler has not been applied (TogglePause). Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-2).RemoveInstructions(6);
            var pauseInstructions = new List<CodeInstruction>()
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, pauseToggleCall),
            };
            match.Insert(pauseInstructions);
            Main.GetLogger<GamePatches>().LogInformation("Transpiler has been applied (TogglePause + BreakGlobalMap). Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(Game), nameof(Game.LoadNewGame), [typeof(BlueprintAreaPreset), typeof(SaveInfo)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Game_LoadNewGame_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(GamePatches), nameof(GamePatches.GetGameId));
            var lookFor = AccessTools.PropertySetter(typeof(Player), nameof(Player.GameId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GamePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-5).RemoveInstructions(5);
            var newInstructions = new List<CodeInstruction>()
            {
                new (OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<GamePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static bool BreakGlobalMap()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = GlobalMapUI.Instance != null
                && ((GlobalMapUI.Instance.m_BtnContinue.GetComponentInChildren<OwlcatButton>()?.Interactable ?? false)
                || (GlobalMapUI.Instance.m_BtnStop.GetComponentInChildren<OwlcatButton>()?.Interactable ?? false));

            return canContinue;
        }

        private static string GetGameId()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Guid.NewGuid().ToString("N");
            }

            var gameId = Main.Multiplayer.GetNewGameSequenceId();
            Main.GetLogger<GamePatches>().LogInformation("Starting new game with predefined Id. GameId={GameId}", gameId);
            return gameId;
        }

        private static void TogglePause(Game game)
        {
            if (!Main.Multiplayer.IsActive)
            {
                game.IsPaused = !game.IsPaused;
                return;
            }

            var isPaused = game.IsPaused;
            var canTogglePause = Main.Multiplayer.TogglePause(isPaused);
            if (canTogglePause)
            {
                game.IsPaused = !game.IsPaused;
            }
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
            // known issues:
            // combat log is not closed during vendor UI
            // party frames display all characters after trading
            // action bar is available during trading
            // FullScreenUI has a separate 'selectedFullScreenCharacter' property, but it's not reliable due to skipped game modes.
            // applying a generic fix for everything (with a hope not to cause extra problems smile) to make sure SelectedUnit is always set if we control atlease 1 character
            var combatLogView = Main.UIAccessor.CombatLogPCView?.ViewModel == null ? null : Main.UIAccessor.CombatLogPCView;
            if (isStart)
            {
                combatLogView?.Hide();
                var needsUpdate = false;
                // capital mode + inventory/etc should show every character
                if (Game.Instance.Player.CapitalPartyMode && !Game.Instance.Vendor.IsTrading)
                {
                    switch (Game.Instance.RootUiContext.InGameVM.StaticPartVM.ServiceWindowsVM.m_FullScreenUIType)
                    {
                        case FullScreenUIType.Inventory:
                        case FullScreenUIType.SpellBook:
                        case FullScreenUIType.Encyclopedia:
                        case FullScreenUIType.Journal:
                        case FullScreenUIType.CharacterScreen:
                        case FullScreenUIType.MythicScreen:
                            var group = SelectionCharacterController.GetGroup(true, false);
                            if (Game.Instance.SelectionCharacter.m_ActualGroup.Count != group.Count)
                            {
                                Game.Instance.SelectionCharacter.m_ActualGroup = group;
                                needsUpdate = true;
                            }
                            break;
                    }
                }

                if (needsUpdate)
                {
                    UpdateSelectedCharacter();
                }

                return;
            }

            combatLogView?.Show();
            UpdateSelectedCharacter();

            if (Game.Instance.Player.CapitalPartyMode)
            {
                Game.Instance.SelectionCharacter.m_NeedUpdate = true;
            }
        }

        private static void UpdateSelectedCharacter()
        {
            if (Game.Instance.Vendor.IsTrading)
            {
                return;
            }

            var selectedCharacter = Game.Instance.Player.CapitalPartyMode || Game.Instance.SelectionCharacter.SelectedUnits?.Count == 0 ? Game.Instance.Player.MainCharacter.Value : Game.Instance.SelectionCharacter.SelectedUnits.FirstOrDefault();
            Game.Instance.SelectionCharacter.SelectedUnit.Value = selectedCharacter;
            Game.Instance.SelectionCharacter.m_FullScreenSelectedUnit = selectedCharacter;
            Game.Instance.SelectionCharacter.SelectionCharacterUpdated.Execute();
        }
    }
}
