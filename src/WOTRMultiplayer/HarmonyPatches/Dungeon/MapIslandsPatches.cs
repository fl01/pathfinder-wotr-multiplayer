using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.MapIslands;
using Kingmaker.UI.MVVM._VM.MapIslands;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Area;

namespace WOTRMultiplayer.HarmonyPatches.Dungeon
{
    [HarmonyPatch]
    public class MapIslandsPatches
    {
        [HarmonyPatch(typeof(MapIslandsPCView), nameof(MapIslandsPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void MapIslandsPCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnTransitionMapShown();
        }

        [HarmonyPatch(typeof(MapIslandsVM), nameof(MapIslandsVM.Close))]
        [HarmonyPrefix]
        public static void MapIslandsVM_Close_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnTransitionMapClosed();
        }

        [HarmonyPatch(typeof(MapIslandItemView), nameof(MapIslandItemView.OnSelect))]
        [HarmonyPrefix]
        public static void MapIslandItemView_OnSelect_Prefix(MapIslandItemView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var islandMapTransition = Main.Mapper.Map<NetworkIslandMapTransition>(__instance.ViewModel.m_IslandState);
            Main.Multiplayer.OnIslandMapEntryChosen(islandMapTransition);
        }

        [HarmonyPatch(typeof(MapIslandItemView), nameof(MapIslandItemView.OnSelect))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MapIslandItemView_OnSelect_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(MapIslandsPatches), nameof(MapIslandsPatches.ShouldShowOneCharacterConfirmationMessage));
            var matcher = new CodeMatcher(instructions, generator);

            var match = matcher.End().CreateLabel(out var endLabel);
            match = match.Advance(-1).SearchBackwards(x => x.opcode == OpCodes.Ret);
            if (match.IsInvalid)
            {
                Main.GetLogger<MapIslandsPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }
            match = match.Advance(1);
            var labels = match.Instruction.ExtractLabels();
            var newInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Call, replaceWith).WithLabels(labels),
                new(OpCodes.Brfalse_S, endLabel)
            };
            match = match.Insert(newInstructions);
            Main.GetLogger<MapIslandsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(MapIslandsPCView), nameof(MapIslandsPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MapIslandsPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(MapIslandsPatches), nameof(MapIslandsPatches.SubscribeEscPress));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<MapIslandsPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match = match.Advance(-7)
                .RemoveInstructions(8)
                .Insert(newInstructions);

            Main.GetLogger<MapIslandsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static bool ShouldShowOneCharacterConfirmationMessage()
        {
            return !Main.Multiplayer.IsActive;
        }

        private static IDisposable SubscribeEscPress(MapIslandsPCView view)
        {
            var subscription = Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || view.m_CloseButton.Interactable)
                {
                    view.ViewModel.Close();
                }
            });

            return subscription;
        }
    }
}
