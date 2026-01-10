using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.ElementsSystem;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Area;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class AreaTransitionGroupCommandPatches
    {
        [HarmonyPatch(typeof(AreaTransitionGroupCommand), nameof(AreaTransitionGroupCommand.ExecuteTransition))]
        [HarmonyPrefix]
        public static bool AreaTransitionGroupCommand_ExecuteTransition_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanInitiateAreaTransitions();
            return canContinue;
        }

        [HarmonyPatch(typeof(AreaTransitionGroupCommand), nameof(AreaTransitionGroupCommand.ExecuteTransition))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityOvertipVM_StartAreaTransition_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(AreaTransitionGroupCommandPatches), nameof(AreaTransitionGroupCommandPatches.OnActionsAreaTransition));
            var lookFor = AccessTools.Method(typeof(ActionList), nameof(ActionList.Run));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<AreaTransitionGroupCommandPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, extraCall),
            };
            match = match.Advance(-2).Insert(newInstructions);
            Main.GetLogger<AreaTransitionGroupCommandPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void OnActionsAreaTransition(AreaTransitionPart areaTransition)
        {
            var currentArea = Game.Instance.CurrentlyLoadedArea;
            var transition = new NetworkAreaTransition
            {
                AreaExitId = areaTransition.View.UniqueId,
                IsActionsTransition = true, // exclusive for transpiler
                From = new NetworkArea { Id = currentArea.AssetGuid.ToString(), Name = currentArea.name },
                To = new NetworkArea { Id = areaTransition.AreaEnterPoint.AssetGuid.ToString(), Name = areaTransition.AreaEnterPoint.name }
            };

            Main.Multiplayer.OnAreaTransition(transition);
        }
    }
}
