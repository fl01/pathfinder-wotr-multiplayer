using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Rest;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RandomEncounters;
using Kingmaker.RandomEncounters.Settings;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.HarmonyPatches.Rolls;

namespace WOTRMultiplayer.HarmonyPatches.Rest
{
    [HarmonyPatch]
    public class RestRandomEncounterContextPatches
    {
        [HarmonyPatch(typeof(RestController), nameof(RestController.TryRollRandomEncounter))]
        [HarmonyPrefix]
        public static void RestController_TryRollRandomEncounter_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }


            Main.Multiplayer.OnBeforeTryRollRandomEncounter();
        }

        [HarmonyPatch(typeof(RestController), nameof(RestController.TryRollRandomEncounter))]
        [HarmonyPostfix]
        public static void RestController_TryRollRandomEncounter_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }


            Main.Multiplayer.OnAfterTryRollRandomEncounter();
        }

        [HarmonyPatch(typeof(RestController), nameof(RestController.TryRollSpecialEncounter))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RestController_TryRollSpecialEncounter_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!ReplaceSpecialEncounterRoll(matcher, target) || !ReplaceTimePassedEncounterRoll(matcher, target))
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Failed to apply all replacements. Target={Target}", target);
                return instructions;
            }

            Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static bool ReplaceSpecialEncounterRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnSpecialEncounterRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        private static bool ReplaceTimePassedEncounterRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnSpecialEncounterHoursPassedRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(float), typeof(float)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        public static int OnSpecialEncounterRoll(int randomMin, int randomMax, BlueprintCampingEncounter encounter)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;

                var encounterKey = encounter.AssetGuid.ToString();
                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.SpecialEncounters.Add(encounterKey, roll);
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded SpecialEncounter. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", encounterKey, roll, randomMin, randomMax);
                    return roll;
                }

                context.PreRecorded.SpecialEncounters.TryGetValue(encounterKey, out var remoteRoll);
                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded SpecialEncounter. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", encounterKey, remoteRoll, randomMin, randomMax);
                return remoteRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public static float OnSpecialEncounterHoursPassedRoll(float randomMin, float randomMax)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;

                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.HoursPassedBeforeEncounter = roll;
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded HoursPassedBeforeEncounter. Roll={Roll}, Min={Min}, Max={Max}", context.Recording.HoursPassedBeforeEncounter, randomMin, randomMax);
                    return context.Recording.HoursPassedBeforeEncounter;
                }

                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded HoursPassedBeforeEncounter. Roll={Roll}, Min={Min}, Max={Max}", context.PreRecorded.HoursPassedBeforeEncounter, randomMin, randomMax);
                return context.PreRecorded.HoursPassedBeforeEncounter;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        [HarmonyPatch(typeof(RestController), nameof(RestController.RollEncounter))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RestController_RollEncounter_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!ReplaceEncounterGuardSlotRoll(matcher, target) || !ReplaceEncounterCamouflageRoll(matcher, target))
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Failed to apply all replacements. Target={Target}", target);
                return instructions;
            }

            Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static bool ReplaceEncounterGuardSlotRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterGuardSlotRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        private static bool ReplaceEncounterCamouflageRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterCamouflageRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        public static int OnEncounterGuardSlotRoll(int randomMin, int randomMax)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;

                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.GuardSlotRoll = roll;
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded GuardSlotRoll. Roll={Roll}, Min={Min}, Max={Max}", context.Recording.GuardSlotRoll, randomMin, randomMax);
                    return context.Recording.GuardSlotRoll;
                }

                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded GuardSlotRoll. Roll={Roll}, Min={Min}, Max={Max}", context.PreRecorded.GuardSlotRoll, randomMin, randomMax);
                return context.PreRecorded.GuardSlotRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public static int OnEncounterCamouflageRoll(int randomMin, int randomMax)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;

                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.CamouflageRoll = roll;
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded CamouflageRoll. Roll={Roll}, Min={Min}, Max={Max}", context.Recording.CamouflageRoll, randomMin, randomMax);
                    return context.Recording.CamouflageRoll;
                }

                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded CamouflageRoll. Roll={Roll}, Min={Min}, Max={Max}", context.PreRecorded.CamouflageRoll, randomMin, randomMax);
                return context.PreRecorded.CamouflageRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        [HarmonyPatch(typeof(RandomEncounterUnitSelector), nameof(RandomEncounterUnitSelector.SelectUnits))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RandomEncounterUnitSelector_SelectUnits_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterRandomUnitSeedRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static int OnEncounterRandomUnitSeedRoll(int randomMin, int randomMax)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;

                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.RandomUnitSeed = roll;
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded RandomUnitSeed. Roll={Roll}", context.Recording.RandomUnitSeed.Value);
                    return context.Recording.RandomUnitSeed.Value;
                }

                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded RandomUnitSeed. Roll={Roll}, Min={Min}, Max={Max}", context.PreRecorded.RandomUnitSeed.Value, randomMin, randomMax);
                return context.PreRecorded.RandomUnitSeed.Value;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        [HarmonyPatch(typeof(RandomEncounterUnitSelector), nameof(RandomEncounterUnitSelector.PlaceUnitsInCamp))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RandomEncounterUnitSelector_PlaceUnitsInCamp_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!ReplacePlaceUnitsInCampRangedRoll(matcher, target)
                || !ReplacePlaceUnitsInCampRangedTargetUnitRoll(matcher, target) // order is important, must be run after previous method
                || !ReplacePlaceUnitsInCampUnitYRoll(matcher, target)
                || !ReplacePlaceUnitsInCampUnitEndPositionRoll(matcher, target))
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Failed to apply all replacements. Target={Target}", target);
                return instructions;
            }

            Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static bool ReplacePlaceUnitsInCampRangedRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterPlaceUnitsInCampRangedRoll));
            var lookFor = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.value));
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Failed {MethodName}. Target={Target}, Position={Position}", MethodBase.GetCurrentMethod().Name, target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 6),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        private static bool ReplacePlaceUnitsInCampUnitYRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterPlaceUnitsInCampUnitYRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(float), typeof(float)]);
            var replacementCounter = 0;
            CodeMatcher match;
            while ((match = matcher.SearchForward(x => x.Calls(lookFor))).IsValid && replacementCounter < 2)
            {
                match.RemoveInstruction();
                var newInstructions = new List<CodeInstruction>()
                {
                    new(OpCodes.Ldloc_S, 6),
                    new(OpCodes.Call, replaceWith),
                };
                match.Insert(newInstructions);
                replacementCounter++;
            }

            const int ExpectedReplacementCounter = 2;
            if (replacementCounter != ExpectedReplacementCounter)
            {
                Main.GetLogger<RuleAttackRollPatches>().LogError("Instructions have not been replaced expected number of times. Target={Target}, Expected={expected}, Current={current}", target, ExpectedReplacementCounter, replacementCounter);
                return false;
            }
            return true;
        }

        private static bool ReplacePlaceUnitsInCampUnitEndPositionRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterPlaceUnitsInCampUnitEndPositionRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(float), typeof(float)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Failed {MethodName}. Target={Target}, Position={Position}", MethodBase.GetCurrentMethod().Name, target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 6),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        private static bool ReplacePlaceUnitsInCampRangedTargetUnitRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterPlaceUnitsInCampRangedTargetUnitRoll));
            var match = matcher.Advance(5);

            if (match.Opcode != OpCodes.Call)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Failed {MethodName}. Target={Target}, Position={Position}", MethodBase.GetCurrentMethod().Name, target, match.Instruction);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 6),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        public static UnitEntityData OnEncounterPlaceUnitsInCampRangedTargetUnitRoll(List<UnitEntityData> units, UnitEntityData unit)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return units.Random();
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;
                if (context.Recording != null)
                {
                    var spawnAt = units.Random();
                    context.Recording.PlaceUnitsInCampRangedTargetRolls.Add(unit.UniqueId, spawnAt.UniqueId);
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded PlaceUnitsInCampRangedTargetRolls. Key={Key}, SpawnAtUnit={SpawnAtUnit}", unit.UniqueId, spawnAt.UniqueId);
                    return spawnAt;
                }

                context.PreRecorded.PlaceUnitsInCampRangedTargetRolls.TryGetValue(unit.UniqueId, out var remoteRoll);
                var remoteSpawnAt = units.FirstOrDefault(u => string.Equals(u.UniqueId, remoteRoll, System.StringComparison.OrdinalIgnoreCase));
                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded PlaceUnitsInCampRangedTargetRolls. Key={Key}, SpawnAtUnit={SpawnAtUnit}", unit.UniqueId, remoteSpawnAt?.UniqueId);
                return remoteSpawnAt;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public static float OnEncounterPlaceUnitsInCampRangedRoll(UnitEntityData unit)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.value;
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;
                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.value;
                    context.Recording.PlaceUnitsInCampRangedRolls.Add(unit.UniqueId, roll);
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded PlaceUnitsInCampRangedRolls. Key={Key}, IsRangedRoll={IsRangedRoll}", unit.UniqueId, roll);
                    return roll;
                }

                context.PreRecorded.PlaceUnitsInCampRangedRolls.TryGetValue(unit.UniqueId, out var remoteRoll);
                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded PlaceUnitsInCampRangedRolls. Key={Key}, IsRangedRoll={IsRangedRoll}", unit.UniqueId, remoteRoll);
                return remoteRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public static float OnEncounterPlaceUnitsInCampUnitYRoll(float randomMin, float randomMax, UnitEntityData unit)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;
                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.PlaceUnitsInCampUnitYRolls.Add(unit.UniqueId, roll);
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded PlaceUnitsInCampUnitYRolls. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", unit.UniqueId, roll, randomMin, randomMax);
                    return roll;
                }

                context.PreRecorded.PlaceUnitsInCampUnitYRolls.TryGetValue(unit.UniqueId, out var remoteRoll);
                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded PlaceUnitsInCampUnitYRolls. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", unit.UniqueId, remoteRoll, randomMin, randomMax);
                return remoteRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public static float OnEncounterPlaceUnitsInCampUnitEndPositionRoll(float randomMin, float randomMax, UnitEntityData unit)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;
                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.PlaceUnitsInCampUnitEndPositionRolls.Add(unit.UniqueId, roll);
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded PlaceUnitsInCampUnitEndPositionRolls. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", unit.UniqueId, roll, randomMin, randomMax);
                    return roll;
                }

                context.PreRecorded.PlaceUnitsInCampUnitEndPositionRolls.TryGetValue(unit.UniqueId, out var remoteRoll);
                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded PlaceUnitsInCampUnitEndPositionRolls. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", unit.UniqueId, remoteRoll, randomMin, randomMax);
                return remoteRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        [HarmonyPatch(typeof(RandomEncounterUnitSelector), nameof(RandomEncounterUnitSelector.PlaceUnitsOutsideOfCamp))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RandomEncounterUnitSelector_PlaceUnitsOutsideOfCamp_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!ReplacePlaceUnitsOutsideOfCampSharedYRoll(matcher, target)
                || !ReplacePlaceUnitsOutsideOfCampUnitYRoll(matcher, target)
                || !ReplacePlaceUnitsOutsideOfCampUnitEndPositionRoll(matcher, target))
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Failed to apply all replacements. Target={Target}", target);
                return instructions;
            }

            Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static bool ReplacePlaceUnitsOutsideOfCampSharedYRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterPlaceUnitsOutsideOfCampSharedYRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(float), typeof(float)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        private static bool ReplacePlaceUnitsOutsideOfCampUnitYRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterPlaceUnitsOutsideOfCampUnitYRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(float), typeof(float)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 4),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }

        private static bool ReplacePlaceUnitsOutsideOfCampUnitEndPositionRoll(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(RestRandomEncounterContextPatches), nameof(OnEncounterPlaceUnitsOutsideOfCampUnitEndPositionRoll));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(float), typeof(float)]);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 4),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            return true;
        }


        public static float OnEncounterPlaceUnitsOutsideOfCampSharedYRoll(float randomMin, float randomMax)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;

                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.PlaceUnitsOutsideOfCampSharedYRoll = roll;
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded PlaceUnitsOutsideOfCampSharedYRoll. Roll={Roll}, Min={Min}, Max={Max}", context.Recording.PlaceUnitsOutsideOfCampSharedYRoll, randomMin, randomMax);
                    return context.Recording.PlaceUnitsOutsideOfCampSharedYRoll;
                }

                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded PlaceUnitsOutsideOfCampSharedYRoll. Roll={Roll}, Min={Min}, Max={Max}", context.PreRecorded.PlaceUnitsOutsideOfCampSharedYRoll, randomMin, randomMax);
                return context.PreRecorded.PlaceUnitsOutsideOfCampSharedYRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public static float OnEncounterPlaceUnitsOutsideOfCampUnitYRoll(float randomMin, float randomMax, int index, IList<UnitEntityData> units)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;
                var unit = units[index];
                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.PlaceUnitsOutsideOfCampUnitYRolls.Add(unit.UniqueId, roll);
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded PlaceUnitsOutsideOfCampUnitYRolls. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", unit.UniqueId, roll, randomMin, randomMax);
                    return roll;
                }

                context.PreRecorded.PlaceUnitsOutsideOfCampUnitYRolls.TryGetValue(unit.UniqueId, out var remoteRoll);
                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded PlaceUnitsOutsideOfCampUnitYRolls. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", unit.UniqueId, remoteRoll, randomMin, randomMax);
                return remoteRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public static float OnEncounterPlaceUnitsOutsideOfCampUnitEndPositionRoll(float randomMin, float randomMax, int index, IList<UnitEntityData> units)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return UnityEngine.Random.Range(randomMin, randomMax);
                }

                var context = Main.Multiplayer.RemoteContext.RandomEncounter;
                var unit = units[index];
                if (context.Recording != null)
                {
                    var roll = UnityEngine.Random.Range(randomMin, randomMax);
                    context.Recording.PlaceUnitsOutsideOfCampUnitEndPositionRolls.Add(unit.UniqueId, roll);
                    Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Recorded PlaceUnitsOutsideOfCampUnitEndPositionRolls. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", unit.UniqueId, roll, randomMin, randomMax);
                    return roll;
                }

                context.PreRecorded.PlaceUnitsOutsideOfCampUnitEndPositionRolls.TryGetValue(unit.UniqueId, out var remoteRoll);
                Main.GetLogger<RestRandomEncounterContextPatches>().LogInformation("Using Prerecorded PlaceUnitsOutsideOfCampUnitEndPositionRolls. Key={Key}, Roll={Roll}, Min={Min}, Max={Max}", unit.UniqueId, remoteRoll, randomMin, randomMax);
                return remoteRoll;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<RestRandomEncounterContextPatches>().LogError(ex, "Error during processing {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }
    }
}
