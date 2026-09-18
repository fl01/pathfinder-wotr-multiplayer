using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Utility;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    /// <summary>
    /// AssetGuid influences randomization of skill checks for Blessings (Islands DLC)
    /// </summary>
    [HarmonyPatch]
    public class InteractionSkillCheckPartPatches
    {
        [HarmonyPatch(typeof(InteractionSkillCheckPart), nameof(InteractionSkillCheckPart.AssetGuid), MethodType.Getter)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> InteractionSkillCheckPart_AssetGuid_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(InteractionSkillCheckPartPatches), nameof(InteractionSkillCheckPartPatches.GenerateAssetGuid));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<InteractionSkillCheckPartPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match = match.RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<InteractionSkillCheckPartPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(InteractionSkillCheckPart), nameof(InteractionSkillCheckPart.OnSettingsDidSet))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> InteractionSkillCheckPart_OnSettingsDidSet_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(InteractionSkillCheckPartPatches), nameof(InteractionSkillCheckPartPatches.RandomizeSkillCheck));
            var matcher = new CodeMatcher(instructions);
            var lookFor = $"{typeof(StatType).FullName} {nameof(LinqExtensions.Random)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));

            if (match.IsInvalid)
            {
                Main.GetLogger<InteractionSkillCheckPartPatches>().LogError("Invalid transpiler position. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };

            match = match.RemoveInstruction().Insert(newInstructions);

            Main.GetLogger<InteractionSkillCheckPartPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static StatType RandomizeSkillCheck(IList<StatType> statTypes, InteractionSkillCheckPart interactionSkillCheckPart)
        {
            if (!Main.Multiplayer.IsActive || statTypes == null || statTypes.Count == 0)
            {
                return statTypes.Random();
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext(SeedKind.Session | SeedKind.LoadedSaveSeed | SeedKind.AreaSeed);
                var owner = interactionSkillCheckPart.Owner;
                var identifier = $"{nameof(InteractionSkillCheckPart)}:{nameof(RandomizeSkillCheck)}:{owner?.UniqueId}:{statTypes.Count}_{seededContext.Id}";
                var index = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, identifier, 0, statTypes.Count);
                var stat = statTypes[index];
                Main.GetLogger<InteractionSkillCheckPartPatches>().LogInformation("SkillCheck Stat has been randomized. Stat={Stat}, MapObjectId={MapObjectId}, Identifier={Identifier}", stat, owner?.UniqueId, identifier);
                return stat;
            }
            catch (Exception ex)
            {
                Main.GetLogger<InteractionSkillCheckPartPatches>().LogError(ex, "Error while randomizing SkillCheck Stat. MapObjectId={MapObjectId}", interactionSkillCheckPart?.Owner?.UniqueId);
                throw;
            }
        }

        private static string GenerateAssetGuid(InteractionSkillCheckPart interactionSkillCheckPart)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Guid.NewGuid().ToString();
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext(SeedKind.Session | SeedKind.LoadedSaveSeed | SeedKind.AreaSeed);
                var owner = interactionSkillCheckPart.Owner;
                var identifier = $"{nameof(InteractionSkillCheckPart)}:{nameof(GenerateAssetGuid)}:{owner?.UniqueId}_{seededContext.Id}";
                var assetGuid = Main.Multiplayer.ValueGenerator.CreateGuid(IdentifierLifetime.Area, identifier);
                Main.GetLogger<InteractionSkillCheckPartPatches>().LogInformation("AssetGuid for MapObject has been generated. AssetGuid={AssetGuid}, MapObjectId={MapObjectId}, Identifier={Identifier}", assetGuid, owner?.UniqueId, identifier);
                return assetGuid.ToString();
            }
            catch (Exception ex)
            {
                Main.GetLogger<InteractionSkillCheckPartPatches>().LogError(ex, "Error while generating AssetGuid for MapObject. MapObjectId={MapObjectId}", interactionSkillCheckPart?.Owner?.UniqueId);
                throw;
            }
        }
    }
}
