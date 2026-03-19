using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Designers.Mechanics.Facts;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Facts
{
    [HarmonyPatch]
    public class ModifyD20Patches
    {
        [HarmonyPatch(typeof(ModifyD20), nameof(ModifyD20.OnEventAboutToTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ModifyD20_OnEventAboutToTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWith = AccessTools.Method(typeof(ModifyD20Patches), nameof(ModifyD20Patches.RollChance));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<ModifyD20Patches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<ModifyD20Patches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int RollChance(int minInclusive, int maxExclusive, ModifyD20 modifyD20)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(ModifyD20)}:{nameof(RollChance)}:{modifyD20.Owner?.UniqueId}_{seededContext.Id}";
                var roll = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minInclusive, maxExclusive);
                Main.GetLogger<ModifyD20Patches>().LogInformation("ModifyD20 has been rolled. UnitId={UnitId}, Roll={Roll}, MinInclusive={MinInclusive}, MaxExclusive={MaxExclusive}, Identifier={Identifier}", modifyD20.Owner?.UniqueId, minInclusive, maxExclusive, identifier);
                return roll;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ModifyD20Patches>().LogError(ex, "Error while rolling unit ModifyD20. UnitId={UnitId}", modifyD20.Owner?.UniqueId);
                throw;
            }
        }
    }
}
