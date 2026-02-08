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
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.RandomIdGeneration
{
    [HarmonyPatch]
    public class RandomElementSelectionPatches
    {
        [HarmonyPatch(typeof(ArmyRoot), nameof(ArmyRoot.SummonTravellingArmy))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyRoot_SummonTravellingArmy_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var travelingCountCall = AccessTools.Method(typeof(RandomElementSelectionPatches), nameof(RandomElementSelectionPatches.GetTravelingArmiesCount));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError("Transpiler has not been applied (TravelingArmiesCount). Target={Target}", target);
                return instructions;
            }

            match = match.RemoveInstruction();
            var travelingArmiesCountInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, travelingCountCall),
            };
            match.Insert(travelingArmiesCountInstructions);

            var randomArmyCall = AccessTools.Method(typeof(RandomElementSelectionPatches), nameof(RandomElementSelectionPatches.GetTravelingArmyRandom));
            match = match.SearchForward(x => x.Is(OpCodes.Newobj, AccessTools.Constructor(typeof(Random), [typeof(int)])));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError("Transpiler has not been applied (RandomArmySelection). Target={Target}", target);
                return instructions;
            }
            var randomArmyInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, randomArmyCall),
            };

            match = match.RemoveInstruction().Insert(randomArmyInstructions);
            Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

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

            Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
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

            Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
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

            Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
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

            var sessionSeed = Main.Multiplayer.GetSessionSeed();
            var combatSeed = Main.Multiplayer.GetCombatSeed();
            var combatTurnSeed = Main.Multiplayer.GetCombatTurnSeed();
            var identifier = $"{nameof(DublicateSpellComponent)}:{nameof(DublicateSpellComponent.GetNewTarget)}:{nameof(GetDuplicateSpellRandom)}:{Game.Instance.Player.GameId}:{ability.Caster?.Unit?.UniqueId}:{ability.NameForAcronym}:{baseTarget.UniqueId}:{sessionSeed}:{combatSeed}:{combatTurnSeed}";
            var random = Main.Multiplayer.ValueGenerator.GetRandom(SeedLifetime.CombatTurn, identifier);
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
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var identifier = $"{nameof(LeadersRoot)}:{nameof(LeadersRoot.GetLeadersForRecruit)}:{nameof(GetLeadersForRecruitRandom)}:{sessionSeed}:{Game.Instance.Player.GameId}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(SeedLifetime.Area, identifier);
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
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var crusadeCombatSeed = Main.Multiplayer.GetCrusadeArmyCombatSeed();
                var identifier = $"{nameof(SquadsActionOnTacticalCombatStart)}:{nameof(SquadsActionOnTacticalCombatStart.HandleCombatStart)}:{nameof(GetSquadsActionOnCombatStartRandom)}:{Game.Instance.Player.GameId}:{sessionSeed}:{crusadeCombatSeed}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(SeedLifetime.Combat, identifier);
                Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Squads action on combat start random has been initialized. Identifier={Identifier}", identifier);
                return random;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError(ex, "Error while initializing squads action random");
                throw;
            }
        }

        private static Random GetTravelingArmyRandom(int weeks, ArmyRoot.ChapterSpawnInfo chapterSpawnInfo)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return new Random(weeks);
            }

            try
            {

                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var identifier = $"{nameof(ArmyRoot)}:{nameof(ArmyRoot.SummonTravellingArmy)}:{nameof(GetTravelingArmyRandom)}:{sessionSeed}:{Game.Instance.Player.GameId}:{chapterSpawnInfo.Chapter}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(SeedLifetime.Persistent, identifier);
                Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Leaders for recruit random has been initialized. Identifier={Identifier}");
                return random;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError(ex, "Error while initializing traveling army random");
                throw;
            }
        }

        private static int GetTravelingArmiesCount(int minInclusive, int maxExclusive, ArmyRoot.ChapterSpawnInfo chapterSpawnInfo)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }
            try
            {

                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var identifier = $"{nameof(ArmyRoot)}:{nameof(ArmyRoot.SummonTravellingArmy)}:{nameof(GetTravelingArmiesCount)}:{sessionSeed}:{Game.Instance.Player.GameId}:{minInclusive}:{maxExclusive}:{chapterSpawnInfo.Chapter}";
                var armiesCount = Main.Multiplayer.ValueGenerator.Range(SeedLifetime.Persistent, identifier, minInclusive, maxExclusive);
                Main.GetLogger<RandomElementSelectionPatches>().LogInformation("Travling armies count has been rolled. Count={Count}, MinInclusive={MinInclusive}, MaxExclusive={MaxExclusive}, Identifier={Identifier}", armiesCount, minInclusive, maxExclusive, identifier);
                return armiesCount;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomElementSelectionPatches>().LogError(ex, "Error while rolling traveling armies count");
                throw;
            }
        }
    }
}
