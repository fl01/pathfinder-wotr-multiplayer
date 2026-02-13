using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.MVVM._PCView.Crusade.Overtips;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._VM.Crusade.Overtips;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class GlobalMapArmyOvertipItemPatches
    {
        [HarmonyPatch(typeof(GlobalMapArmyOvertipItemPCView), nameof(GlobalMapArmyOvertipItemPCView.CheckMerge))]
        [HarmonyPostfix]
        public static void GlobalMapArmyOvertipItemPCView_CheckMerge_Postfix(GlobalMapArmyOvertipItemPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var isInteractable = Main.Multiplayer.CanControlGlobalMap();
            __instance.m_MergeButton.Interactable = isInteractable;
            __instance.m_LevelUpButton.Interactable = isInteractable;
        }

        [HarmonyPatch(typeof(GlobalMapArmyOvertipsVM), nameof(GlobalMapArmyOvertipsVM.MergeArmies))]
        [HarmonyPrefix]
        public static void GlobalMapArmyOvertipsVM_MergeArmies_Prefix(GlobalMapArmyOvertipsVM __instance)
        {
            var selectedArmy = Game.Instance.GlobalMapController.SelectedArmy;
            if (!Main.Multiplayer.IsActive || selectedArmy == null || selectedArmy.View == null || !__instance.m_ArmiesForMerge.Any())
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapMergeArmies();
        }

        [HarmonyPatch(typeof(GlobalMapArmyOvertipItemVM), nameof(GlobalMapArmyOvertipItemVM.OnLevelUpClick))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapArmyOvertipItemVM_OnLevelUpClick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(GlobalMapArmyOvertipItemPatches), nameof(GlobalMapArmyOvertipItemPatches.OnArmyLeaderLevelup));
            var lookFor = AccessTools.Field(typeof(ArmySquadsVM), nameof(ArmySquadsVM.SplitVM));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.End().Advance(-1);

            if (match.Instruction.opcode != OpCodes.Pop)
            {
                Main.GetLogger<GlobalMapArmyOvertipItemPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstruction = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, extraCall),
            };
            match = match.Insert(newInstruction);
            Main.GetLogger<GlobalMapArmyOvertipItemPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void OnArmyLeaderLevelup(GlobalMapArmyOvertipItemVM overtipItemVM)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapArmy = new NetworkGlobalMapArmy { Id = overtipItemVM.ArmyState.Id };
            Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderLevelingStarted(globalMapArmy);
        }
    }
}
