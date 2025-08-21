using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.HarmonyPatches.Rest;

namespace WOTRMultiplayer.HarmonyPatches.Units
{
    [HarmonyPatch]
    public class SaddledUnitControllerPatches
    {
        /// <summary>
        /// IsDirectlyControllable must be true for correct saddled movement outside of the combat
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(SaddledUnitController), nameof(SaddledUnitController.TickDelegateRiderToMount))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SaddledUnitController_TickDelegateRiderToMount_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(SaddledUnitControllerPatches), nameof(SaddledUnitControllerPatches.IsControlledByPlayers));
            var lookFor = AccessTools.PropertyGetter(typeof(UnitEntityData), nameof(UnitEntityData.IsDirectlyControllable));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<SaddledUnitControllerPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<SaddledUnitControllerPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static bool IsControlledByPlayers(UnitEntityData unit)
        {
            return unit.IsDirectlyControllable ||
                Main.Multiplayer.IsActive && Main.Multiplayer.IsControlledByPlayers(unit.UniqueId);
        }
    }
}
