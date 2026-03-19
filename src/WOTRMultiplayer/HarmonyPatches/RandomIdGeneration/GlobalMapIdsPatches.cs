using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies;
using Kingmaker.Armies.Blueprints;
using Kingmaker.Armies.State;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Globalmap.State;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.Settlements;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.RandomIdGeneration
{
    [HarmonyPatch]
    public class GlobalMapIdsPatches
    {
        [HarmonyPatch(typeof(GlobalMapState), nameof(GlobalMapState.CreateArmy), [typeof(ArmyFaction), typeof(BlueprintArmyPreset), typeof(GlobalMapPosition), typeof(bool), typeof(bool)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapState_CreateArmy_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(GlobalMapIdsPatches), nameof(GlobalMapIdsPatches.GetNewArmyId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldloc_1),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_3),
                new(OpCodes.Ldarg_S, 4),
                new(OpCodes.Ldarg_S, 5),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<GlobalMapIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ArmyLeader), MethodType.Constructor, [typeof(BlueprintArmyLeader), typeof(ArmyFaction)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyLeader_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(GlobalMapIdsPatches), nameof(GlobalMapIdsPatches.GetNewArmyLeaderId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<GlobalMapIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ArmyRoot), nameof(ArmyRoot.SummonTravellingArmy))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyRoot_SummonTravellingArmy_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var travelingCountCall = AccessTools.Method(typeof(GlobalMapIdsPatches), nameof(GlobalMapIdsPatches.GetTravelingArmiesCount));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError("Transpiler has not been applied (TravelingArmiesCount). Target={Target}", target);
                return instructions;
            }

            match = match.RemoveInstruction();
            var travelingArmiesCountInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, travelingCountCall),
            };
            match.Insert(travelingArmiesCountInstructions);

            var randomArmyCall = AccessTools.Method(typeof(GlobalMapIdsPatches), nameof(GlobalMapIdsPatches.GetTravelingArmyRandom));
            match = match.SearchForward(x => x.Is(OpCodes.Newobj, AccessTools.Constructor(typeof(Random), [typeof(int)])));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError("Transpiler has not been applied (RandomArmySelection). Target={Target}", target);
                return instructions;
            }
            var randomArmyInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, randomArmyCall),
            };

            match = match.RemoveInstruction().Insert(randomArmyInstructions);
            Main.GetLogger<GlobalMapIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(SettlementState), MethodType.Constructor, [typeof(BlueprintSettlement), typeof(BlueprintGlobalMapPoint)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SettlementState_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(GlobalMapIdsPatches), nameof(GlobalMapIdsPatches.GetNewSettlementId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<GlobalMapIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static string GetNewSettlementId(BlueprintSettlement blueprintSettlement, BlueprintGlobalMapPoint blueprintGlobalMapPoint)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Guid.NewGuid().ToString();
            }

            try
            {
                string id = null;
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{blueprintSettlement.AssetGuid}:{blueprintGlobalMapPoint.AssetGuid}_{seededContext.Id}";
                while (string.IsNullOrEmpty(id))
                {
                    id = Main.Multiplayer.ValueGenerator.CreateGuid(seededContext.Lifetime, identifier).ToString();
                    var settlement = KingdomState.Instance.SettlementsManager.m_SettlementStates.FirstOrDefault(a => string.Equals(a.UniqueId, id, StringComparison.OrdinalIgnoreCase));

                    if (settlement != null)
                    {
                        id = null;
                        continue;
                    }
                }

                Main.GetLogger<GlobalMapIdsPatches>().LogInformation("Settlement Id has been generated. Id={Id}, RawIdentifier={RawIdentifier}", id, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError(ex, "Error while generating new army Id");
                throw;
            }
        }

        private static string GetNewArmyLeaderId(BlueprintArmyLeader blueprint, ArmyFaction faction)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Guid.NewGuid().ToString();
            }

            try
            {
                string id = null;
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{blueprint.AssetGuid}:{faction}_{seededContext.Id}";
                while (string.IsNullOrEmpty(id))
                {
                    id = Main.Multiplayer.ValueGenerator.CreateGuid(seededContext.Lifetime, identifier).ToString();
                    var army = Game.Instance.Player.ArmyLeadersManager.m_Leaders.FirstOrDefault(a => string.Equals(a.Guid, id, StringComparison.OrdinalIgnoreCase))
                        ?? (Main.UIAccessor.ArmyCartBuyLeaderPCView?.m_Leaders?.Select(x => x.ViewModel?.m_Leader) ?? []).FirstOrDefault(a => string.Equals(a.Guid, id, StringComparison.OrdinalIgnoreCase));

                    if (army != null)
                    {
                        id = null;
                        continue;
                    }
                }

                Main.GetLogger<GlobalMapIdsPatches>().LogInformation("Army LeaderId has been generated. Id={Id}, RawIdentifier={RawIdentifier}", id, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError(ex, "Error while generating new army Id");
                throw;
            }
        }

        private static string GetNewArmyId(ArmyNameWithIndex armyNameWithIndex, ArmyFaction armyFaction, BlueprintArmyPreset armyPreset, GlobalMapPosition position, bool isGarrison, bool isTraveling)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Guid.NewGuid().ToString();
            }

            // the idea is to generate deterministic GUIDs based on army data.
            // each area reload will reset the seed sequence, so we will eventually reuse old IDs for new armies.
            // hopefully, this shouldn't be a problem, and the game doesn't use 'historic' army IDs for anything
            try
            {
                string id = null;
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{armyNameWithIndex.ArmyName}:{armyNameWithIndex.ArmyIndex}:{armyFaction}:{armyPreset?.AssetGuid.ToString()}:{position?.Location?.name}:{isGarrison}:{isTraveling}_{seededContext.Id}";
                while (string.IsNullOrEmpty(id))
                {
                    id = Main.Multiplayer.ValueGenerator.CreateGuid(seededContext.Lifetime, identifier).ToString();
                    var army = GlobalMapController.State.Armies.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (army != null)
                    {
                        id = null;
                        continue;
                    }
                }

                Main.GetLogger<GlobalMapIdsPatches>().LogInformation("ArmyId has been generated. Id={Id}, RawIdentifier={RawIdentifier}", id, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError(ex, "Error while generating new army Id");
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
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(ArmyRoot)}:{nameof(ArmyRoot.SummonTravellingArmy)}:{nameof(GetTravelingArmiesCount)}:{Game.Instance.Player.GameId}:{minInclusive}:{maxExclusive}:{chapterSpawnInfo.Chapter}_{seededContext.Id}";
                var armiesCount = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Persistent, identifier, minInclusive, maxExclusive);
                Main.GetLogger<GlobalMapIdsPatches>().LogInformation("Travling armies count has been rolled. Count={Count}, MinInclusive={MinInclusive}, MaxExclusive={MaxExclusive}, Identifier={Identifier}", armiesCount, minInclusive, maxExclusive, identifier);
                return armiesCount;
            }
            catch (Exception ex)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError(ex, "Error while rolling traveling armies count");
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
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(ArmyRoot)}:{nameof(ArmyRoot.SummonTravellingArmy)}:{nameof(GetTravelingArmyRandom)}:{Game.Instance.Player.GameId}:{chapterSpawnInfo.Chapter}_{seededContext.Id}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(IdentifierLifetime.Persistent, identifier);
                Main.GetLogger<GlobalMapIdsPatches>().LogInformation("Traveling army random has been initialized. Identifier={Identifier}");
                return random;
            }
            catch (Exception ex)
            {
                Main.GetLogger<GlobalMapIdsPatches>().LogError(ex, "Error while initializing traveling army random");
                throw;
            }
        }
    }
}
