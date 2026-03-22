using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
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
    public class EntitiesIdsPatches
    {
        private readonly static MethodInfo _getNewIdLookupMethod = AccessTools.Method(typeof(Player), nameof(Player.GetNewUniqueId));

        [HarmonyPatch(typeof(Player), nameof(Player.CreateCustomCompanion))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Player_CreateCustomCompanion_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Game), nameof(Game.CreateUnitVacuum));
            var replaceWith = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.CreateCompanionUnit));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError("Unable to find CreateUnitVacuum call. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldloc_3),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-2).RemoveInstructions(3).Insert(newInstructions);

            Main.GetLogger<EntitiesIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
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

            Main.GetLogger<EntitiesIdsPatches>().LogError("Player.GetNewUniqueId should never be called, Result={Result}, StackTrace={StackTrace}", __result, Environment.StackTrace);
        }

        [HarmonyPatch(typeof(AbilityData), MethodType.Constructor, argumentTypes: [typeof(BlueprintAbility), typeof(UnitDescriptor), typeof(Ability), typeof(BlueprintSpellbook)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> AbilityData_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.GetAbilityDataEntityId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
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

            Main.GetLogger<EntitiesIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(AreaEffectsController), nameof(AreaEffectsController.Spawn), [typeof(MechanicsContext), typeof(BlueprintAbilityAreaEffect), typeof(TargetWrapper), typeof(TimeSpan?), typeof(bool)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> AreaEffectsController_Spawn_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.GetNewAreaEffectUniqueId));
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
            var replaceWith = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.GetNewEntityUniqueId));
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
            var replaceWith = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.GetNewUnitUniqueId));
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
            var voiceCall = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.SelectUnitVoice));
            var leftHandedCall = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.SelectLeftHanded));
            var lookFor = AccessTools.Method(typeof(UnitCustomizationPreset), nameof(UnitCustomizationPreset.SelectVoice));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError("Invalid transpiler position. Target={Target}", target);
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
            Main.GetLogger<EntitiesIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static BlueprintUnitAsksList SelectUnitVoice(BlueprintUnit blueprintUnit, Gender gender)
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
                var voiceIndex = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, uniqueId, 0, voices.Count);
                var voiceReference = voices[voiceIndex];
                var voice = voiceReference.Get();
                Main.GetLogger<EntitiesIdsPatches>().LogDebug("Unit voice has been selected. Id={Id}, Gender={Gender}, Voice={Voice}", uniqueId, gender, voice.name);

                return voice;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Unable to select unit voice");
                throw;
            }
        }

        private static bool SelectLeftHanded(BlueprintUnit blueprintUnit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return blueprintUnit.CustomizationPreset.SelectLeftHanded();
            }

            try
            {
                var uniqueId = blueprintUnit.name;
                var leftHandedRoll = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, uniqueId, 0f, 1f);
                var isLeftHanded = leftHandedRoll <= blueprintUnit.CustomizationPreset.Distribution.LeftHandedChance;
                Main.GetLogger<EntitiesIdsPatches>().LogDebug("Unit handedness has been selected. Id={Id}, Roll={Roll}, IsLeftHanded={IsLeftHanded}", uniqueId, leftHandedRoll, isLeftHanded);
                return isLeftHanded;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Unable to select unit handedness");
                throw;
            }
        }

        [HarmonyPatch(typeof(EntityCreationController), nameof(EntityCreationController.ChangeUnitBlueprint))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityCreationController_ChangeUnitBlueprint_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.GetNewChangeBlueprintUniqueId));
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
            var replaceWith = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.GetNewEntityFactUniqueId));
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
            var replaceWith = AccessTools.Method(typeof(EntitiesIdsPatches), nameof(EntitiesIdsPatches.GetNewItemEntityId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor)).Advance(-1);
            if (match.IsInvalid)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError("Unable to find Guid.NewGuid() call. Target={Target}", target);
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

            Main.GetLogger<EntitiesIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
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
                Main.GetLogger<EntitiesIdsPatches>().LogError("Invalid transpiler position. Target={Target}, OpCode={OpCode}", target, match.Instruction.opcode);
                return matcher.Instructions();
            }

            match = match.RemoveInstructions(3).Insert(newInstructions);
            Main.GetLogger<EntitiesIdsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static UnitEntityData CreateCompanionUnit(BlueprintUnit blueprintUnit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Game.Instance.CreateUnitVacuum(blueprintUnit);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{nameof(Game.CreateUnitVacuum)}:{blueprintUnit.name}:{blueprintUnit.AssetGuid}_{seededContext.Id}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(IdType.CustomCompanionUnit, Game.Instance.Player.GameId, identifier);
                var unit = new UnitEntityData(id, isInGame: true, blueprintUnit);
                return unit;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Error while generating custom companion unit Id. Type={Type}", IdType.CustomCompanionUnit);
                throw;
            }
        }

        private static string GetNewAreaEffectUniqueId(AreaEffectView areaEffectView)
        {
            try
            {
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{areaEffectView.GetType().Name}:{areaEffectView.name}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(IdType.AreaEffect, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Error while generating unique Id. Type={Type}", IdType.AreaEffect);
                throw;
            }
        }

        private static string GetNewEntityUniqueId(EntityViewBase prefab)
        {
            try
            {
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{prefab.GetType().Name}:{prefab.name}";
                var type = prefab is DroppedLoot ? IdType.DroppedLoot : IdType.EntityView;
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(type, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Error while generating unique Id. Type={Type}", IdType.EntityView);
                throw;
            }
        }

        private static string GetNewUnitUniqueId(BlueprintUnit unit, UnitEntityView prefab)
        {
            try
            {
                var rawIdentifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{unit.AssetGuid}:{prefab.name}:{unit?.CharacterName}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(IdType.Unit, Game.Instance.Player.GameId, rawIdentifier);
                Main.GetLogger<EntitiesIdsPatches>().LogDebug("UnitId has been generated. GameId={GameId}, RawIdentifier={RawIdentifier}, Id={Id}", Game.Instance.Player.GameId, rawIdentifier, id);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Error while generating new unit Id. Type={Type}", IdType.Unit);
                throw;
            }
        }

        private static string GetNewEntityFactUniqueId(EntityFact fact)
        {
            try
            {
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{fact.GetType().Name}:{fact.NameForAcronym}:{fact.Owner?.UniqueId}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(IdType.Fact, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Error while generating new entity fact Id. Type={Type}", IdType.Fact);
                throw;
            }
        }

        private static string GetNewChangeBlueprintUniqueId(UnitEntityData unitEntityData, BlueprintUnit blueprintUnit)
        {
            try
            {
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{unitEntityData.CharacterName}:{blueprintUnit?.name}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(IdType.ChangeBlueprintUnit, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Error while generating 'changeBlueprint' Id. Type={Type}", IdType.ChangeBlueprintUnit);
                throw;
            }
        }

        private static string GetNewItemEntityId(BlueprintItem blueprintItem)
        {
            try
            {
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{blueprintItem?.name ?? "null-item"}:{blueprintItem?.ItemType}:{blueprintItem?.MiscellaneousType}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(IdType.ItemEntity, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Error while generating new item entity Id. Type={Type}", IdType.ItemEntity);
                throw;
            }
        }

        private static string GetAbilityDataEntityId(BlueprintAbility blueprint, UnitDescriptor caster, Ability fact, BlueprintSpellbook blueprintSpellbook)
        {
            try
            {
                var identifier = $"{CommonTranspilerReplacements.GetSharedIdentifierPart()}:{blueprint.NameForAcronym}:{caster?.Unit?.UniqueId}:{fact?.UniqueId}:{blueprintSpellbook?.name}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(IdType.AbilityData, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<EntitiesIdsPatches>().LogError(ex, "Error while generating unique Id. Type={Type}", IdType.AbilityData);
                throw;
            }
        }
    }
}
