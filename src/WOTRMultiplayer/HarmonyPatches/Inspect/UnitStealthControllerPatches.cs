using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Inspect;

namespace WOTRMultiplayer.HarmonyPatches.Inspect
{
    [HarmonyPatch]
    public class UnitStealthControllerPatches
    {
        [HarmonyPatch(typeof(UnitStealthController), nameof(UnitStealthController.TickUnit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitStealthController_TickUnit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(UnitStealthControllerPatches), nameof(TriggerRuleCachedPerceptionCheck));
            var matcher = new CodeMatcher(instructions);
            // TODO: need to check how to properly find a call to generic method. Sticking to shitty lookup for now :/
            //var lookFor = AccessTools.FirstMethod(typeof(Rulebook), x => x.Name == nameof(Rulebook.Trigger) && x.GetParameters().Length == 1);
            var lookFor = $"{typeof(RuleCachedPerceptionCheck).FullName} {nameof(Rulebook.Trigger)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitStealthControllerPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);

            Main.GetLogger<UnitStealthControllerPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static RuleCachedPerceptionCheck TriggerRuleCachedPerceptionCheck(RuleCachedPerceptionCheck ruleCachedPerceptionCheck, UnitEntityData unitInStealth)
        {
            if (!Main.Multiplayer.IsActive || ruleCachedPerceptionCheck.Initiator.CachedPerceptionRoll > 0)
            {
                return Rulebook.Trigger(ruleCachedPerceptionCheck);
            }

            var canMakeStealthCheck = Main.Multiplayer.CanMakeStealthPerceptionCheck();
            if (!canMakeStealthCheck)
            {
                ruleCachedPerceptionCheck.SetSuccess(false);
                return ruleCachedPerceptionCheck;
            }

            var originalDC = ruleCachedPerceptionCheck.OriginalDC;
            var newlyRolledCheck = Rulebook.Trigger(ruleCachedPerceptionCheck);
            // clients don't care about failed rolls
            if (newlyRolledCheck.Success)
            {
                // rule resets CachedPerceptionRoll to 0 if it was success, so I see two options here
                // 1. use base.D20 to get roll -
                //    it doesn't look very stable, some code paths don't use overriden RollD20 to 'pre-roll' it based on CachedPerceptionRoll
                //    also I'm lacking understanding if those code paths are actually used in this RuleCachedPerceptionCheck case
                // 2. force success by using a big number as a roll (e.g. 999)
                //    looks most promising option as actual value doesn't not really matter if it was a success,
                //    the only problem is that it will be visibile for a clients in combat log
                // sticking to option 1 for now, will be replaced with option 2 if needed

                // newlyRolledCheck.D20 -> 999
                var check = new NetworkStealthPerceptionCheck
                {
                    InitiatorId = newlyRolledCheck.Initiator.UniqueId,
                    StealthedUnitId = unitInStealth.UniqueId,
                    Roll = newlyRolledCheck.D20,
                    IsSuccess = newlyRolledCheck.Success,
                    DC = originalDC,
                    IgnoreDifficultyBonusToDC = newlyRolledCheck.IgnoreDifficultyBonusToDC,
                    IsTargetInvisible = newlyRolledCheck.IsTargetInvisible
                };
                Main.Multiplayer.OnStealthPerceptionCheckRolled(check);
            }

            return newlyRolledCheck;
        }
    }
}
