using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Items
{
    [HarmonyPatch]
    public class IdentificationPatches
    {
        [HarmonyPatch(typeof(TricksterArcanaAdditionalEnchantments), nameof(TricksterArcanaAdditionalEnchantments.OnItemIdentified))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TricksterArcanaAdditionalEnchantments_OnItemIdentified_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(IdentificationPatches), nameof(IdentificationPatches.GetRandomTricksterEnchantment));
            var matcher = new CodeMatcher(instructions);
            // TODO: need to check how to properly find a call to generic extension method (LinqExtensions.Random) with few overloads. Sticking to shitty lookup for now :/
            var lookFor = $"{typeof(BlueprintItemEnchantment).FullName} {nameof(LinqExtensions.Random)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));

            if (match.IsInvalid)
            {
                Main.GetLogger<IdentificationPatches>().LogError("Invalid transpiler position. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith),
            };

            match = match.RemoveInstruction().Insert(newInstructions);

            Main.GetLogger<IdentificationPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static BlueprintItemEnchantment GetRandomTricksterEnchantment(List<BlueprintItemEnchantment> enchantments, ItemEntity item, UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return enchantments.Random();
            }

            if (enchantments.Count == 0)
            {
                return null;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                // itemId is volatile and could be different across the players
                var identifier = $"{nameof(TricksterArcanaAdditionalEnchantments)}:{nameof(GetRandomTricksterEnchantment)}:{item.NameForAcronym}:{item.EnchantmentValue}:{item.Cost}:{unit.UniqueId}_{seededContext.Id}";
                var index = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, 0, enchantments.Count);
                var enchantment = enchantments[index];
                Main.GetLogger<IdentificationPatches>().LogInformation("Random Trickster Enchantment has been rolled. Index={Index}, EnchantmentName={EnchantmentName}, UnitId={UnitId}, Identifier={Identifier}", index, enchantment.name, unit.UniqueId, identifier);
                return enchantment;
            }
            catch (Exception ex)
            {
                Main.GetLogger<IdentificationPatches>().LogError(ex, "Error while selecting random trickster item enchantment. ItemName={ItemName}, UnitId={UnitId}", item.NameForAcronym, unit.UniqueId);
                throw;
            }
        }
    }
}
