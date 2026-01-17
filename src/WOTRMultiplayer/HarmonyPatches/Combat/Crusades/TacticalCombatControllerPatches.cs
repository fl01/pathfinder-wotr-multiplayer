using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Armies.TacticalCombat.Controllers;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatControllerPatches
    {
        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.Setup))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TacticalCombatController_Setup_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.PropertySetter(typeof(TacticalCombatData), nameof(TacticalCombatData.Seed));
            var replaceWith = AccessTools.Method(typeof(TacticalCombatControllerPatches), nameof(TacticalCombatControllerPatches.SetCrusadeArmyCombatSeed));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<TacticalCombatControllerPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var labels = match.Instruction.ExtractLabels();
            var newInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Call, replaceWith).WithLabels(labels),
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<TacticalCombatControllerPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }


        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.ReportIfFreezed))]
        [HarmonyPrefix]
        public static bool TacticalCombatController_ReportIfFreezed_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.ProcessBattleEnd))]
        [HarmonyPrefix]
        public static void TacticalCombatController_ProcessBattleEnd_Prefix()
        {
            if (!Main.Multiplayer.IsActive || Game.Instance.Player.TacticalCombatResults != null)
            {
                return;
            }

            Main.Multiplayer.OnCrusadeArmyCombatEnded();
        }

        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.Setup))]
        [HarmonyPostfix]
        public static void TacticalCombatController_Setup_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnCrusadeArmyCombatInitialized();
        }

        private static void SetCrusadeArmyCombatSeed(TacticalCombatData data, int seed)
        {
            if (!Main.Multiplayer.IsActive)
            {
                data.Seed = seed;
                return;
            }

            var multiplayerSeed = Main.Multiplayer.GetCrusadeArmyCombatSeed() ?? seed;
            Main.GetLogger<TacticalCombatControllerPatches>().LogInformation("Crusade Army Combat seed has been overriden. OldValue={OldValue}, NewValue={NewValue}", seed, multiplayerSeed);
            data.Seed = multiplayerSeed;
        }
    }
}
