using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.ContextActions
{
    [HarmonyPatch]
    public class ContextActionOnRandomAreaTargetPatches
    {
        [HarmonyPatch(typeof(ContextActionOnRandomAreaTarget), nameof(ContextActionOnRandomAreaTarget.RunAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ContextActionOnRandomAreaTarget_RunAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(ContextActionOnRandomAreaTargetPatches), nameof(ContextActionOnRandomAreaTargetPatches.SelectRandomTarget));
            var matcher = new CodeMatcher(instructions);
            // TODO: need to check how to properly find a call to generic extension method (LinqExtensions.Random) with few overloads. Sticking to shitty lookup for now :/
            var lookFor = $"{typeof(UnitEntityData).FullName} {nameof(LinqExtensions.Random)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));

            if (match.IsInvalid)
            {
                Main.GetLogger<ContextActionOnRandomAreaTargetPatches>().LogError("Invalid transpiler position. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };

            match = match.RemoveInstruction().Insert(newInstructions);

            Main.GetLogger<ContextActionOnRandomAreaTargetPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static UnitEntityData SelectRandomTarget(IList<UnitEntityData> targets, ContextActionOnRandomAreaTarget contextActionOnRandom)
        {
            if (!Main.Multiplayer.IsActive || targets == null || targets.Count == 0)
            {
                return targets.Random();
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var unitId = contextActionOnRandom.AbilityContext.MaybeOwner?.UniqueId;
                var targetId = contextActionOnRandom.AbilityContext.MainTarget?.Unit?.UniqueId;
                var abilityName = contextActionOnRandom.AbilityContext.NameForAcronym;
                var attackNumber = contextActionOnRandom.AbilityContext.AttackRoll?.RuleAttackWithWeapon?.AttackNumber ?? -1;
                var units = string.Join(",", targets.Select(x => x.UniqueId));
                var identifier = $"{nameof(ContextActionOnRandomAreaTarget)}:{nameof(SelectRandomTarget)}:{units}:{unitId}:{targetId}:{abilityName}:{attackNumber}_{seededContext.Lifetime}";
                var index = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, 0, targets.Count);
                var unit = targets[index];
                Main.GetLogger<ContextActionOnRandomAreaTargetPatches>().LogInformation("ContextActionOnRandomAreaTarget has been rolled. UnitId={UnitId}, Identifier={Identifier}", unitId, identifier);
                return unit;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ContextActionOnRandomAreaTargetPatches>().LogError(ex, "Error while rolling random action");
                throw;
            }
        }
    }
}
