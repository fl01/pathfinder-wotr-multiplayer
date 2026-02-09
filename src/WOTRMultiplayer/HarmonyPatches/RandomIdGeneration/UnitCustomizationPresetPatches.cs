using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Customization;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.RandomIdGeneration
{
    [HarmonyPatch]
    public class UnitCustomizationPresetPatches
    {
        [HarmonyPatch(typeof(UnitCustomizationPreset), nameof(UnitCustomizationPreset.SelectVariation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitCustomizationPreset_SelectVariation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(UnitCustomizationPresetPatches), nameof(UnitCustomizationPresetPatches.SelectUnitVariation));
            var matcher = new CodeMatcher(instructions);
            // TODO: need to check how to properly find a call to generic extension method (LinqExtensions.Random) with few overloads. Sticking to shitty lookup for now :/
            var lookFor = $"{typeof(UnitCustomizationVariation).FullName} {nameof(LinqExtensions.Random)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));

            if (match.IsInvalid)
            {
                Main.GetLogger<UnitCustomizationPresetPatches>().LogError("Invalid transpiler position. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };

            match = match.RemoveInstruction().Insert(newInstructions);

            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static UnitCustomizationVariation SelectUnitVariation(IList<UnitCustomizationVariation> variations, BlueprintUnit blueprintUnit)
        {
            if (!Main.Multiplayer.IsActive || variations == null || variations.Count == 0)
            {
                return variations.Random();
            }

            try
            {
                var uniqueId = $"{blueprintUnit.name}:{nameof(UnitCustomizationPreset.SelectVariation)}";
                var variationIndex = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, uniqueId, 0, variations.Count);
                var variation = variations[variationIndex];
                Main.GetLogger<UnitCustomizationPresetPatches>().LogDebug("Unit variation has been selected. Id={Id}, Race={Race}, Gender={Gender}, PrefabId={PrefabId}", uniqueId, variation.Race, variation.Gender, variation.Prefab.AssetId);

                return variation;
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCustomizationPresetPatches>().LogError(ex, "Failed to select unit variation");
                throw;
            }
        }
    }
}
