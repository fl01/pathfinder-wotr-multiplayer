using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat.LeaderSkills;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.RandomIdGeneration
{
    [HarmonyPatch]
    public class RandomElementSelectionPatches
    {
        [HarmonyPatch(typeof(DublicateSpellComponent), nameof(DublicateSpellComponent.GetNewTarget))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> DublicateSpellComponent_GetNewTarget_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomElementSelectionPatches), nameof(RandomElementSelectionPatches.GetDuplicateSpellRandom));
            var matcher = new CodeMatcher(instructions);

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith)
            };

            if (!ReplaceRandomInitialization(matcher, newInstructions))
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return matcher.Instructions();
            }

            Main.GetLogger<RandomElementSelectionPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(LeadersRoot), nameof(LeadersRoot.GetLeadersForRecruit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> LeadersRoot_GetLeadersForRecruit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomElementSelectionPatches), nameof(RandomElementSelectionPatches.GetLeadersForRecruitRandom));
            var matcher = new CodeMatcher(instructions);

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Call, replaceWith)
            };

            if (!ReplaceRandomInitialization(matcher, newInstructions))
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return matcher.Instructions();
            }

            Main.GetLogger<RandomElementSelectionPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(SquadsActionOnTacticalCombatStart), nameof(SquadsActionOnTacticalCombatStart.HandleCombatStart))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SquadsActionOnTacticalCombatStart_HandleCombatStart_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomElementSelectionPatches), nameof(RandomElementSelectionPatches.GetLeadersForRecruitRandom));
            var matcher = new CodeMatcher(instructions);

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Call, replaceWith)
            };

            if (!ReplaceRandomInitialization(matcher, newInstructions))
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return matcher.Instructions();
            }

            Main.GetLogger<RandomElementSelectionPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static bool ReplaceRandomInitialization(CodeMatcher matcher, List<CodeInstruction> codeInstructions)
        {
            var match = matcher.SearchForward(x => x.Is(OpCodes.Newobj, AccessTools.Constructor(typeof(Random), [])));
            if (match.IsInvalid)
            {
                return false;
            }

            match = match.RemoveInstruction().Insert(codeInstructions);
            return true;
        }

        private static Random GetDuplicateSpellRandom(AbilityData ability, UnitEntityData baseTarget)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return new Random();
            }

            var seededContext = Main.Multiplayer.GetSeededContext();
            var identifier = $"{nameof(DublicateSpellComponent)}:{nameof(DublicateSpellComponent.GetNewTarget)}:{nameof(GetDuplicateSpellRandom)}:{Game.Instance.Player.GameId}:{ability.Caster?.Unit?.UniqueId}:{ability.NameForAcronym}:{baseTarget.UniqueId}_{seededContext.Id}";
            var random = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
            Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Duplicate spell target random has been initialized. Identifier={Identifier}", identifier);
            return random;
        }

        private static Random GetLeadersForRecruitRandom()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return new Random();
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(LeadersRoot)}:{nameof(LeadersRoot.GetLeadersForRecruit)}:{nameof(GetLeadersForRecruitRandom)}_{seededContext.Id}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
                Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Leaders for recruit random has been initialized. Identifier={Identifier}", identifier);
                return random;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError(ex, "Error while initializing leaders random");
                throw;
            }
        }

        private static Random GetSquadsActionOnCombatStartRandom()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return new Random();
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(SquadsActionOnTacticalCombatStart)}:{nameof(SquadsActionOnTacticalCombatStart.HandleCombatStart)}:{nameof(GetSquadsActionOnCombatStartRandom)}_{seededContext.Id}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
                Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Squads action on combat start random has been initialized. Identifier={Identifier}", identifier);
                return random;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError(ex, "Error while initializing squads action random");
                throw;
            }
        }
    }
}
