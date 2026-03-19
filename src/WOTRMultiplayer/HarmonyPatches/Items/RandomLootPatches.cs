using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Loot;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Items
{
    [HarmonyPatch]
    public class RandomLootPatches
    {
        [HarmonyPatch(typeof(TrashLootSettings), nameof(TrashLootSettings.Fill))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TrashLootSettings_Fill_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomLootPatches), nameof(RandomLootPatches.GetBlueprintLootRandomizator));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Ldnull);
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomLootPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);

            match = matcher.SearchForward(x => x.opcode == OpCodes.Ldnull);
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomLootPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }
            match = match.RemoveInstruction().Insert(newInstructions);

            Main.GetLogger<RandomLootPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(LootRandomItem), nameof(LootRandomItem.AddItemsTo))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> LootRandomItem_AddItemsTo_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWith = AccessTools.Method(typeof(RandomLootPatches), nameof(RandomLootPatches.GetRandomLootItem));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomLootPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<RandomLootPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(LootItemsPackVariable), nameof(LootItemsPackVariable.AddItemsTo))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> LootItemsPackVariable_AddItemsTo_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWith = AccessTools.Method(typeof(RandomLootPatches), nameof(RandomLootPatches.GetRandomLootItemsPack));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomLootPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<RandomLootPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static Func<int, int, int> GetBlueprintLootRandomizator(BlueprintLoot blueprintLoot)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return null;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(BlueprintLoot)}:{nameof(GetBlueprintLootRandomizator)}:{Game.Instance.Player.GameId}:{blueprintLoot.Area?.name}:{blueprintLoot.Area?.AssetGuid.ToString()}:{blueprintLoot.Cost}_{seededContext.Id}";
                var randomizer = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
                Main.GetLogger<RandomLootPatches>().LogInformation("Randomizer for BlueprintLoot has been initialized. Identifier={Identifier}", identifier);
                return randomizer.Next;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomLootPatches>().LogError(ex, "Error while initializing randomizer for BlueprintLoot");
                throw;
            }
        }

        private static int GetRandomLootItem(int minInclusive, int maxExclusive, LootRandomItem lootRandomItem)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {
                var ownerBlueprintId = lootRandomItem.OwnerBlueprint.AssetGuid.ToString();
                var ownerBlueprintName = lootRandomItem.OwnerBlueprint.name;
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(LootRandomItem)}:{nameof(GetRandomLootItem)}:{ownerBlueprintId}:{ownerBlueprintName}_{seededContext.Id}";
                var roll = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minInclusive, maxExclusive);
                Main.GetLogger<RandomLootPatches>().LogInformation("Random unit loot item index has been rolled. Roll={Roll}, Identifier={Identifier}, MinInclusive={MinInclusive}, MaxExclusive={MaxExclusive}", roll, identifier, minInclusive, maxExclusive);
                return roll;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomLootPatches>().LogError(ex, "Error while rolling unit loot");
                throw;
            }
        }

        private static int GetRandomLootItemsPack(int minInclusive, int maxExclusive, LootItemsPackVariable lootRandomItem)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {
                var ownerBlueprintId = lootRandomItem.OwnerBlueprint.AssetGuid.ToString();
                var ownerBlueprintName = lootRandomItem.OwnerBlueprint.name;
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(LootItemsPackVariable)}:{nameof(GetRandomLootItemsPack)}:{ownerBlueprintId}:{ownerBlueprintName}_{seededContext.Id}";
                var roll = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minInclusive, maxExclusive);
                Main.GetLogger<RandomLootPatches>().LogInformation("Random unitpack loot item index has been rolled. Roll={Roll}, Identifier={Identifier}, MinInclusive={MinInclusive}, MaxExclusive={MaxExclusive}", roll, identifier, minInclusive, maxExclusive);
                return roll;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomLootPatches>().LogError(ex, "Error while rolling unitpack loot");
                throw;
            }
        }
    }
}
