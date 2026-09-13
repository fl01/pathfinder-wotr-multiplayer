using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DungeonArchitect;
using HarmonyLib;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    /// <summary>
    /// BuffSpellSubstitution (WildMagic) - Islands, The Lord of Nothing
    /// </summary>
    [HarmonyPatch]
    public class UnitUseAbilitySubstitutionPatches
    {
        [HarmonyPatch(typeof(UnitUseAbility), nameof(UnitUseAbility.SubstituteSpell), [typeof(AbilityData), typeof(TargetWrapper)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitUseAbility_SubstituteSpell_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(UnitUseAbilitySubstitutionPatches), nameof(UnitUseAbilitySubstitutionPatches.ShuffleSpells));
            var matcher = new CodeMatcher(instructions);
            var lookFor = $"Void {nameof(LinqExtensions.Shuffle)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor, StringComparison.OrdinalIgnoreCase) ?? false));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitUseAbilitySubstitutionPatches>().LogError("Invalid transpiler position. Target={Target}", target);
                PatchesUtils.Dump(matcher);
                return matcher.Instructions();
            }

            match = match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);

            Main.GetLogger<UnitUseAbilitySubstitutionPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void ShuffleSpells(UnitUseAbility command, IList<BlueprintAbility> blueprintAbilities)
        {
            if (!Main.Multiplayer.IsActive)
            {
                blueprintAbilities.Shuffle();
                return;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var unitId = command.Executor.UniqueId;
                var identifier = $"{nameof(UnitUseAbility.SubstituteSpell)}:{unitId}_{seededContext.Id}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
                blueprintAbilities.Shuffle(random);
                Main.GetLogger<UnitUseAbilitySubstitutionPatches>().LogInformation("Substitution spells have been shuffled. UnitId={UnitId}, Identifier={Identifier}", unitId, identifier);
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitUseAbilitySubstitutionPatches>().LogError(ex, "Error while shuffling abilities for substitution");
                throw;
            }
        }
    }
}
