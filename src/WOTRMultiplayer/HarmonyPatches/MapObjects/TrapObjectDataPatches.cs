using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.View.MapObjects.Traps;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class TrapObjectDataPatches
    {
        [HarmonyPatch(typeof(TrapObjectData), nameof(TrapObjectData.Interact))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TrapObjectData_Interact_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(TrapObjectDataPatches), nameof(TrapObjectDataPatches.OnTrapDisarmRolled));
            var lookFor = AccessTools.Method(typeof(GameHelper), nameof(GameHelper.TriggerSkillCheck));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<TrapObjectDataPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, extraCall),
            };
            match = match.Advance(2).Insert(newInstructions);
            Main.GetLogger<TrapObjectDataPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void OnTrapDisarmRolled(TrapObjectData trapObjectData, UnitEntityData unit, RuleSkillCheck ruleSkillCheck)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var trapDisarm = new NetworkTrapDisarm
            {
                IsSuccess = ruleSkillCheck.Success,
                MapObject = new NetworkMapObject
                {
                    Id = trapObjectData.UniqueId,
                    Position = trapObjectData.Position.ToNetworkVector3()
                },
                Roll = ruleSkillCheck.RollResult,
                UnitId = unit.UniqueId
            };

            Main.Multiplayer.OnTrapDisarmRolled(trapDisarm);
        }
    }
}
