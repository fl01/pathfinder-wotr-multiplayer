using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.ElementsSystem;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.ContextActions
{
    [HarmonyPatch]
    public class ContextActionRandomizePatches
    {
        [HarmonyPatch(typeof(ContextActionRandomize), nameof(ContextActionRandomize.RunAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ContextActionRandomize_RunAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookForSeed = AccessTools.Field(typeof(ContextActionRandomize), nameof(ContextActionRandomize.Seed));
            var replaceSeedWith = AccessTools.Method(typeof(ContextActionRandomizePatches), nameof(ContextActionRandomizePatches.EvaluateSeed));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.LoadsField(lookForSeed));
            if (match.IsInvalid)
            {
                Main.GetLogger<ContextActionRandomizePatches>().LogError("Transpiler has not been applied (Seed). Target={Target}", target);
                return instructions;
            }
            var seedInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceSeedWith),
            };
            match.Advance(1).RemoveInstruction().Insert(seedInstructions);

            var lookForSalt = AccessTools.Field(typeof(ContextActionRandomize), nameof(ContextActionRandomize.Salt));
            var replaceSaltWith = AccessTools.Method(typeof(ContextActionRandomizePatches), nameof(ContextActionRandomizePatches.EvaluateSalt));
            match = match.SearchForward(x => x.LoadsField(lookForSalt));
            if (match.IsInvalid)
            {
                Main.GetLogger<ContextActionRandomizePatches>().LogError("Transpiler has not been applied (Salt). Target={Target}", target);
                return instructions;
            }
            var saltInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceSaltWith),
            };
            match.Advance(1).RemoveInstruction().Insert(saltInstructions);

            var lookForRandom = $"ActionWrapper {nameof(LinqExtensions.Random)}";
            var replaceRandom = AccessTools.Method(typeof(ContextActionRandomizePatches), nameof(ContextActionRandomizePatches.RollRandomAction));
            match = match.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookForRandom) ?? false));
            if (match.IsInvalid)
            {
                Main.GetLogger<ContextActionRandomizePatches>().LogError("Transpiler has not been applied (Random). Target={Target}", target);
                return instructions;
            }

            var randomInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceRandom),
            };
            match.RemoveInstruction().Insert(randomInstructions);

            var lookForWeightedRandom = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWeighted = AccessTools.Method(typeof(ContextActionRandomizePatches), nameof(ContextActionRandomizePatches.RollRandomWeightedActionIndex));
            match = match.SearchForward(x => x.Calls(lookForWeightedRandom));
            if (match.IsInvalid)
            {
                Main.GetLogger<ContextActionRandomizePatches>().LogError("Transpiler has not been applied (WeightedRandom). Target={Target}", target);
                return instructions;
            }
            var weightedRandomInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWeighted),
            };
            match.RemoveInstruction().Insert(weightedRandomInstructions);

            Main.GetLogger<ContextActionRandomizePatches>().LogDebug("Transpiler has been applied (Seed + Salt + Random + WeightedRandom). Target={Target}", target);
            return matcher.Instructions();
        }

        private static ContextActionRandomize.ActionWrapper RollRandomAction(IList<ContextActionRandomize.ActionWrapper> actions, ContextActionRandomize contextActionRandomize)
        {
            if (!Main.Multiplayer.IsActive || actions == null || actions.Count == 0)
            {
                return actions.Random();
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var unitId = contextActionRandomize.AbilityContext?.MaybeOwner?.UniqueId;
                var targetId = contextActionRandomize.AbilityContext?.MainTarget?.Unit?.UniqueId;
                var abilityName = contextActionRandomize.AbilityContext?.NameForAcronym;
                var attackNumber = contextActionRandomize.AbilityContext?.AttackRoll?.RuleAttackWithWeapon?.AttackNumber ?? -1;
                var identifier = $"{nameof(ContextActionRandomize)}.{nameof(ContextActionRandomize.ActionWrapper)}:{nameof(RollRandomAction)}:{unitId}:{targetId}:{abilityName}:{attackNumber}_{seededContext.Id}";
                var index = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, 0, actions.Count);
                var action = actions[index];
                var actionsToRun = string.Join(",", action?.Action?.Actions?.Select(x => x.ToString()));
                Main.GetLogger<ContextActionRandomizePatches>().LogInformation("ContextActionRandomize Random Wrapper has been rolled. UnitId={UnitId}, ActionsToRun={ActionsToRun}, Identifier={Identifier}", unitId, actionsToRun, identifier);
                return action;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ContextActionRandomizePatches>().LogError(ex, "Error while rolling random action");
                throw;
            }
        }

        private static int RollRandomWeightedActionIndex(int minInclusive, int maxExclusive, ContextActionRandomize contextActionRandomize)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var unitId = contextActionRandomize.AbilityContext?.MaybeOwner?.UniqueId ?? contextActionRandomize.Context?.MaybeCaster?.UniqueId;
                var targetId = contextActionRandomize.AbilityContext?.MainTarget?.Unit?.UniqueId;
                var abilityName = contextActionRandomize.AbilityContext?.NameForAcronym;
                var attackNumber = contextActionRandomize.AbilityContext?.AttackRoll?.RuleAttackWithWeapon?.AttackNumber ?? -1;
                var identifier = $"{nameof(ContextActionRandomize)}:{nameof(RollRandomWeightedActionIndex)}:{minInclusive}:{maxExclusive}:{unitId}:{targetId}:{abilityName}:{attackNumber}_{seededContext.Id}";
                var index = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minInclusive, maxExclusive);
                Main.GetLogger<ContextActionRandomizePatches>().LogInformation("ContextActionRandomize WeightedRandom has been rolled. UnitId={UnitId}, Index={Index}, Identifier={Identifier}", unitId, index, identifier);
                return index;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ContextActionRandomizePatches>().LogError(ex, "Error while rolling random action");
                throw;
            }
        }

        private static int EvaluateSalt(Evaluator<int> salt, ContextActionRandomize contextActionRandomize)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return salt.GetValue();
            }

            // TODO: check if this needs to by synced
            var value = salt.GetValue();
            Main.GetLogger<ContextActionRandomizePatches>().LogWarning("ContextActionRandomize Salt value evaluated. Result={Result}, UnitId={UnitId}, AbilityName={AbilityName}", value, contextActionRandomize.AbilityContext?.Caster?.UniqueId, contextActionRandomize.AbilityContext?.NameForAcronym);
            return value;
        }

        private static int EvaluateSeed(Evaluator<int> seed, ContextActionRandomize contextActionRandomize)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return seed.GetValue();
            }

            // TODO: check if this needs to by synced
            var value = seed.GetValue();
            Main.GetLogger<ContextActionRandomizePatches>().LogWarning("ContextActionRandomize Seed value evaluated. Result={Result}, UnitId={UnitId}, AbilityName={AbilityName}", value, contextActionRandomize.AbilityContext?.Caster?.UniqueId, contextActionRandomize.AbilityContext?.NameForAcronym);
            return value;
        }
    }
}
