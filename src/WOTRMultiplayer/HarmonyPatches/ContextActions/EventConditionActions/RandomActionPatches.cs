using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.ContextActions.EventConditionActions
{
    [HarmonyPatch]
    public class RandomActionPatches
    {
        [HarmonyPatch(typeof(RandomAction), nameof(RandomAction.RunAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RandomAction_RunAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var lookForSeed = AccessTools.Field(typeof(RandomAction), nameof(RandomAction.Seed));
            var replaceSeedWith = AccessTools.Method(typeof(RandomActionPatches), nameof(RandomActionPatches.EvaluateSeed));
            var match = matcher.SearchForward(x => x.LoadsField(lookForSeed));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomActionPatches>().LogError("Transpiler has not been applied (Seed). Target={Target}", target);
                return instructions;
            }
            var seedInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceSeedWith),
            };
            match.Advance(1).RemoveInstruction().Insert(seedInstructions);

            var lookForSalt = AccessTools.Field(typeof(RandomAction), nameof(RandomAction.Salt));
            var replaceSaltWith = AccessTools.Method(typeof(RandomActionPatches), nameof(RandomActionPatches.EvaluateSalt));
            match = match.SearchForward(x => x.LoadsField(lookForSalt));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomActionPatches>().LogError("Transpiler has not been applied (Salt). Target={Target}", target);
                return instructions;
            }
            var saltInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceSaltWith),
            };
            match.Advance(1).RemoveInstruction().Insert(saltInstructions);

            var lookForWeight = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWeightWith = AccessTools.Method(typeof(RandomActionPatches), nameof(RandomActionPatches.GetRandomActionWeight));
            match = match.SearchForward(x => x.Calls(lookForWeight));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomActionPatches>().LogError("Transpiler has not been applied (Weight). Target={Target}", target);
                return instructions;
            }

            var weightInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWeightWith),
            };
            match = match.RemoveInstruction().Insert(weightInstructions);
            Main.GetLogger<RandomActionPatches>().LogDebug("Transpiler has been applied (Weight + Seed + Salt). Target={Target}", target);
            return matcher.Instructions();
        }

        private static int GetRandomActionWeight(int minInclusive, int maxExclusive, RandomAction randomAction)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(RandomAction)}:{nameof(GetRandomActionWeight)}:{randomAction.Owner?.name}:{minInclusive}:{maxExclusive}_{seededContext.Id}";
                var weight = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minInclusive, maxExclusive);
                Main.GetLogger<RandomActionPatches>().LogInformation("RandomAction weight has been rolled. Weight={Weight}, Identifier={Identifier}", weight, identifier);
                return weight;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomActionPatches>().LogError(ex, "Error while rolling random action weight");
                throw;
            }
        }

        private static int EvaluateSalt(Evaluator<int> salt, RandomAction randomAction)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return salt.GetValue();
            }

            // TODO: check if this needs to by synced
            var value = salt.GetValue();
            Main.GetLogger<RandomActionPatches>().LogWarning("RandomAction Salt value evaluated. Salt={Salt},  Owner={Owner}", value, randomAction.Owner?.name);
            return value;
        }

        private static int EvaluateSeed(Evaluator<int> seed, RandomAction randomAction)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return seed.GetValue();
            }

            // TODO: check if this needs to by synced
            var value = seed.GetValue();
            Main.GetLogger<RandomActionPatches>().LogWarning("RandomAction Seed value evaluated. Seed={Seed}, Owner={Owner}", value, randomAction.Owner?.name);
            return value;
        }
    }
}
