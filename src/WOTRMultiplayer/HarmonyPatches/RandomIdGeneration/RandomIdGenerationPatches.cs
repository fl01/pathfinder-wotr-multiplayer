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
using Kingmaker.Armies.TacticalCombat.Controllers;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.Items;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Customization;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.View.MapObjects;
using Kingmaker.Visual.Sound;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.RandomIdGeneration
{
    [HarmonyPatch]
    public class RandomIdGenerationPatches
    {
        private readonly static MethodInfo _getNewIdLookupMethod = AccessTools.Method(typeof(Player), nameof(Player.GetNewUniqueId));

        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.CreateUnit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TacticalCombatController_CreateUnit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewArmyUnitId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_3),
                new(OpCodes.Ldarg_S, 4),
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, replaceWith)
            };
            match = match.RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.CreateCustomCompanion))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Player_CreateCustomCompanion_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Game), nameof(Game.CreateUnitVacuum));
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.CreateCompanionUnit));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Unable to find CreateUnitVacuum call. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldloc_3),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-2).RemoveInstructions(3).Insert(newInstructions);

            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.GetNewUniqueId))]
        [HarmonyPostfix]
        public static void Player_GetNewUniqueId_Postfix(ref string __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.GetLogger<RandomIdGenerationPatches>().LogError("Player.GetNewUniqueId should never be called, Result={Result}, StackTrace={StackTrace}", __result, Environment.StackTrace);
        }

        [HarmonyPatch(typeof(ArmyLeader), MethodType.Constructor, [typeof(BlueprintArmyLeader), typeof(ArmyFaction)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyLeader_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewArmyLeaderId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(GlobalMapState), nameof(GlobalMapState.CreateArmy), [typeof(ArmyFaction), typeof(BlueprintArmyPreset), typeof(GlobalMapPosition), typeof(bool), typeof(bool)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapState_CreateArmy_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewArmyId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
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
            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(AbilityData), MethodType.Constructor, argumentTypes: [typeof(BlueprintAbility), typeof(UnitDescriptor), typeof(Ability), typeof(BlueprintSpellbook)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> AbilityData_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetAbilityDataEntityId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
                return matcher.Instructions();
            }

            match.RemoveInstructions(5);
            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_3),
                new(OpCodes.Ldarg_S, 4),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);

            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(AreaEffectsController), nameof(AreaEffectsController.Spawn), [typeof(MechanicsContext), typeof(BlueprintAbilityAreaEffect), typeof(TargetWrapper), typeof(TimeSpan?), typeof(bool)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> AreaEffectsController_Spawn_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewAreaEffectUniqueId));
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_2),
                new(OpCodes.Call, replaceWith),
            };
            return PatchPlayerIdGeneration(target, instructions, newInstructions);
        }

        [HarmonyPatch(typeof(EntityCreationController), nameof(EntityCreationController.SpawnEntity))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityCreationController_SpawnEntity_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewEntityUniqueId));
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, replaceWith),
            };

            return PatchPlayerIdGeneration(target, instructions, newInstructions);
        }

        /// <summary>
        /// UniqueId only
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(EntityCreationController), nameof(EntityCreationController.SpawnUnit), [typeof(BlueprintUnit), typeof(UnitEntityView), typeof(UnityEngine.Vector3), typeof(UnityEngine.Quaternion), typeof(SceneEntitiesState), typeof(string)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityCreationController_SpawnUnit_Overload1_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewUnitUniqueId));
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith),
            };
            return PatchPlayerIdGeneration(target, instructions, newInstructions);
        }

        /// <summary>
        /// voice + isLeftHanded
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(EntityCreationController), nameof(EntityCreationController.SpawnUnit), [typeof(BlueprintUnit), typeof(UnityEngine.Vector3), typeof(UnityEngine.Quaternion), typeof(SceneEntitiesState), typeof(string)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityCreationController_SpawnUnit_Overload2_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var voiceCall = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.SelectUnitVoice));
            var leftHandedCall = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.SelectLeftHanded));
            var lookFor = AccessTools.Method(typeof(UnitCustomizationPreset), nameof(UnitCustomizationPreset.SelectVoice));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Invalid transpiler position. Target={Target}", target);
                return matcher.Instructions();
            }

            match = match.Advance(-3)
                .RemoveInstructions(1)
                .Advance(2)
                .RemoveInstruction();
            var voiceInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, voiceCall),
            };
            match = match.Insert(voiceInstructions);

            // lefthanded
            match = match.Advance(3).RemoveInstructions(2);
            var leftHandedInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, leftHandedCall),
            };
            match = match.Insert(leftHandedInstructions);
            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static BlueprintUnitAsksList SelectUnitVoice(BlueprintUnit blueprintUnit, Gender gender)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return blueprintUnit.CustomizationPreset.SelectVoice(gender);
            }

            try
            {
                var voices = gender == Gender.Male ? blueprintUnit.CustomizationPreset.MaleVoices : blueprintUnit.CustomizationPreset.FemaleVoices;
                if (voices.Count == 0)
                {
                    return null;
                }

                var uniqueId = blueprintUnit.name;
                var voiceIndex = Main.Multiplayer.ValueGenerator.Range(SeedLifetime.Area, uniqueId, 0, voices.Count);
                var voiceReference = voices[voiceIndex];
                var voice = voiceReference.Get();
                Main.GetLogger<RandomIdGenerationPatches>().LogDebug("Unit voice has been selected. Id={Id}, Gender={Gender}, Voice={Voice}", uniqueId, gender, voice.name);

                return voice;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Unable to select unit voice");
                throw;
            }
        }

        public static bool SelectLeftHanded(BlueprintUnit blueprintUnit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return blueprintUnit.CustomizationPreset.SelectLeftHanded();
            }

            try
            {
                var uniqueId = blueprintUnit.name;
                var leftHandedRoll = Main.Multiplayer.ValueGenerator.Range(SeedLifetime.Area, uniqueId, 0f, 1f);
                var isLeftHanded = leftHandedRoll <= blueprintUnit.CustomizationPreset.Distribution.LeftHandedChance;
                Main.GetLogger<RandomIdGenerationPatches>().LogDebug("Unit handedness has been selected. Id={Id}, Roll={Roll}, IsLeftHanded={IsLeftHanded}", uniqueId, leftHandedRoll, isLeftHanded);
                return isLeftHanded;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Unable to select unit handedness");
                throw;
            }
        }

        [HarmonyPatch(typeof(EntityCreationController), nameof(EntityCreationController.ChangeUnitBlueprint))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityCreationController_ChangeUnitBlueprint_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewChangeBlueprintUniqueId));
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith),
            };
            return PatchPlayerIdGeneration(target, instructions, newInstructions);
        }

        [HarmonyPatch(typeof(EntityFact), nameof(EntityFact.Attach))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityFact_Attach_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewEntityFactUniqueId));
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            return PatchPlayerIdGeneration(target, instructions, newInstructions);
        }

        [HarmonyPatch(typeof(ItemEntity), MethodType.Constructor, [typeof(BlueprintItem)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ItemEntity_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewItemEntityId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor)).Advance(-1);
            if (match.IsInvalid)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
                return matcher.Instructions();
            }

            match.RemoveInstructions(6);
            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);

            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static IEnumerable<CodeInstruction> PatchPlayerIdGeneration(string target, IEnumerable<CodeInstruction> instructions, List<CodeInstruction> newInstructions)
        {
            var matcher = new CodeMatcher(instructions);
            var match = matcher
                .SearchForward(x => x.Calls(_getNewIdLookupMethod))
                .Advance(-2);

            if (match.Instruction.opcode != OpCodes.Call)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Invalid transpiler position. Target={Target}, OpCode={OpCode}", target, match.Instruction.opcode);
                return matcher.Instructions();
            }

            match = match.RemoveInstructions(3).Insert(newInstructions);
            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static UnitEntityData CreateCompanionUnit(BlueprintUnit blueprintUnit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Game.Instance.CreateUnitVacuum(blueprintUnit);
            }

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                if (sessionSeed == null)
                {
                    Main.GetLogger<RandomIdGenerationPatches>().LogError("Session seed is unavailable");
                    return Game.Instance.CreateUnitVacuum(blueprintUnit);
                }

                var identifier = $"{GetCommonIdPart()}:{nameof(Game.CreateUnitVacuum)}:{blueprintUnit.name}:{blueprintUnit.AssetGuid}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.CustomCompanionUnit, Game.Instance.Player.GameId, identifier);
                var unit = new UnitEntityData(id, isInGame: true, blueprintUnit);
                return unit;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating custom companion unit Id. Type={Type}", UniqueIdType.CustomCompanionUnit);
                throw;
            }
        }

        public static string GetNewAreaEffectUniqueId(AreaEffectView areaEffectView)
        {
            try
            {
                var identifier = $"{GetCommonIdPart()}:{areaEffectView.GetType().Name}:{areaEffectView.name}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.AreaEffect, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating unique Id. Type={Type}", UniqueIdType.AreaEffect);
                throw;
            }
        }

        public static string GetNewEntityUniqueId(EntityViewBase prefab)
        {
            try
            {
                var identifier = $"{GetCommonIdPart()}:{prefab.GetType().Name}:{prefab.name}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.EntityView, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating unique Id. Type={Type}", UniqueIdType.EntityView);
                throw;
            }
        }

        public static string GetNewUnitUniqueId(BlueprintUnit unit, UnitEntityView prefab)
        {
            try
            {
                var rawIdentifier = $"{GetCommonIdPart()}:{prefab.name}:{unit?.CharacterName}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.Unit, Game.Instance.Player.GameId, rawIdentifier);
                Main.GetLogger<RandomIdGenerationPatches>().LogDebug("UnitId has been generated. GameId={GameId}, RawIdentifier={RawIdentifier}, Id={Id}", Game.Instance.Player.GameId, rawIdentifier, id);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating new unit Id. Type={Type}", UniqueIdType.Unit);
                throw;
            }
        }

        public static string GetNewEntityFactUniqueId(EntityFact fact)
        {
            try
            {
                var identifier = $"{GetCommonIdPart()}:{fact.GetType().Name}:{fact.NameForAcronym}:{fact.Owner?.UniqueId}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.Fact, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating new entity fact Id. Type={Type}", UniqueIdType.Fact);
                throw;
            }
        }

        public static string GetNewChangeBlueprintUniqueId(UnitEntityData unitEntityData, BlueprintUnit blueprintUnit)
        {
            try
            {
                var identifier = $"{GetCommonIdPart()}:{unitEntityData.CharacterName}:{blueprintUnit?.name}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.ChangeBlueprintUnit, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating 'changeBlueprint' Id. Type={Type}", UniqueIdType.ChangeBlueprintUnit);
                throw;
            }
        }

        public static string GetNewItemEntityId(BlueprintItem blueprintItem)
        {
            try
            {
                var identifier = $"{GetCommonIdPart()}:{blueprintItem.NameForAcronym}:{blueprintItem.ItemType}:{blueprintItem.MiscellaneousType}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.ItemEntity, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating new item entity Id. Type={Type}", UniqueIdType.ItemEntity);
                throw;
            }
        }

        public static string GetNewArmyUnitId(GlobalMapArmyState globalMapArmyState, SquadState squadState, string groupId, RegionId regionId, BlueprintFaction faction)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Guid.NewGuid().ToString();
            }

            try
            {
                var crusadeArmyCombatSeed = Game.Instance.TacticalCombat.Data.Seed;
                var leader = globalMapArmyState.Data.Leader;
                var armyName = globalMapArmyState.Data.ArmyName;
                var rawIdentifier = $"{GetCommonIdPart()}:{globalMapArmyState.Id}:{globalMapArmyState.ArmyType}:{armyName?.ArmyName}:{armyName?.ArmyIndex}:{squadState.Size}:{squadState.Unit.name}:{groupId}:{regionId}:{leader?.Blueprint.name}:{faction.name}:{crusadeArmyCombatSeed}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.CrusadeArmyCombatUnit, Game.Instance.Player.GameId, rawIdentifier);
                Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Army UnitId has been generated. GameId={GameId}, Seed={Seed}, RawIdentifier={RawIdentifier}, Id={Id}", Game.Instance.Player.GameId, crusadeArmyCombatSeed, rawIdentifier, id);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating new army unit Id");
                throw;
            }
        }

        public static string GetNewArmyLeaderId(BlueprintArmyLeader blueprint, ArmyFaction faction)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Guid.NewGuid().ToString();
            }

            try
            {
                string id = null;
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var identifier = $"{GetCommonIdPart()}:{blueprint.AssetGuid}:{faction}:{sessionSeed.Value}";
                while (string.IsNullOrEmpty(id))
                {
                    id = Main.Multiplayer.ValueGenerator.CreateGuid(SeedLifetime.Area, identifier).ToString();
                    var army = Game.Instance.Player.ArmyLeadersManager.m_Leaders.FirstOrDefault(a => string.Equals(a.Guid, id, StringComparison.OrdinalIgnoreCase))
                        ?? (Main.UIAccessor.GlobalMapPCView?.m_BuyLeaderPCView?.m_Leaders?.Select(x => x.ViewModel?.m_Leader) ?? []).FirstOrDefault(a => string.Equals(a.Guid, id, StringComparison.OrdinalIgnoreCase));

                    if (army != null)
                    {
                        id = null;
                        continue;
                    }
                }

                Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Army LeaderId has been generated. GameId={GameId}, Identifier={Identifier}, Id={Id}", Game.Instance.Player.GameId, identifier, id);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating new army Id");
                throw;
            }
        }

        public static string GetNewArmyId(ArmyNameWithIndex armyNameWithIndex, ArmyFaction armyFaction, BlueprintArmyPreset armyPreset, GlobalMapPosition position, bool isGarrison, bool isTraveling)
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
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var identifier = $"{GetCommonIdPart()}:{armyNameWithIndex.ArmyName}:{armyNameWithIndex.ArmyIndex}:{armyFaction}:{armyPreset?.AssetGuid.ToString()}:{position?.Location?.name}:{isGarrison}:{isTraveling}:{sessionSeed.Value}";
                while (string.IsNullOrEmpty(id))
                {
                    id = Main.Multiplayer.ValueGenerator.CreateGuid(SeedLifetime.Area, identifier).ToString();
                    var army = GlobalMapController.State.Armies.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (army != null)
                    {
                        id = null;
                        continue;
                    }
                }

                Main.GetLogger<RandomIdGenerationPatches>().LogInformation("ArmyId has been generated. GameId={GameId}, Identifier={Identifier}, Id={Id}", Game.Instance.Player.GameId, identifier, id);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating new army Id");
                throw;
            }
        }

        public static string GetAbilityDataEntityId(BlueprintAbility blueprint, UnitDescriptor caster, Ability fact, BlueprintSpellbook blueprintSpellbook)
        {
            try
            {
                var identifier = $"{GetCommonIdPart()}:{blueprint.NameForAcronym}:{caster?.Unit?.UniqueId}:{fact?.UniqueId}:{blueprintSpellbook?.name}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.AbilityData, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating unique Id. Type={Type}", UniqueIdType.AbilityData);
                throw;
            }
        }

        private static string GetCommonIdPart()
        {
            var area = Game.Instance.CurrentlyLoadedArea?.name ?? "no-area";
            var gameId = Game.Instance.Player?.GameId;
            return $"{area}:{gameId}";
        }
    }
}
