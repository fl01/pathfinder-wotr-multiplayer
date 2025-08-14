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
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Random;

namespace WOTRMultiplayer.HarmonyPatches.RandomIdGeneration
{
    [HarmonyPatch]
    public class RandomIdGenerationPatches
    {
        public readonly static MethodInfo _lookUpMethod = AccessTools.Method(typeof(Player), nameof(Player.GetNewUniqueId));

        [HarmonyPatch(typeof(Player), nameof(Player.GetNewUniqueId))]
        [HarmonyPostfix]
        public static void Player_GetNewUniqueId_Postfix(Player __instance, ref string __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.GetLogger<RandomIdGenerationPatches>().LogError("Player.GetNewUniqueId should never be called, Result={result}, StackTrace={stackTrace}", __result, Environment.StackTrace);
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
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Unable to find Guid.NewGuid() call. Target={target}", target);
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

            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
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

        [HarmonyPatch(typeof(EntityCreationController), nameof(EntityCreationController.SpawnUnit), [typeof(BlueprintUnit), typeof(UnitEntityView), typeof(UnityEngine.Vector3), typeof(UnityEngine.Quaternion), typeof(SceneEntitiesState), typeof(string)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityCreationController_SpawnUnit_Transpiler(IEnumerable<CodeInstruction> instructions)
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
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Unable to find Guid.NewGuid() call. Target={target}", target);
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

            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

        private static IEnumerable<CodeInstruction> PatchPlayerIdGeneration(string target, IEnumerable<CodeInstruction> instructions, List<CodeInstruction> newInstructions)
        {
            var matcher = new CodeMatcher(instructions);
            var match = matcher
                .SearchForward(x => x.Calls(_lookUpMethod))
                .Advance(-2);

            if (match.Instruction.opcode != OpCodes.Call)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError("Invalid transpiler position. Target={target}, OpCode={opCode}", target, match.Instruction.opcode);
                return matcher.Instructions();
            }

            match.RemoveInstructions(3);
            match.Insert(newInstructions);
            Main.GetLogger<RandomIdGenerationPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
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
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating {type}", UniqueIdType.AreaEffect);
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
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating {type}", UniqueIdType.EntityView);
                throw;
            }
        }

        public static string GetNewUnitUniqueId(BlueprintUnit unit, UnitEntityView prefab)
        {
            try
            {
                var identifier = $"{GetCommonIdPart()}:{prefab.name}:{unit?.CharacterName}";
                var id = Main.Multiplayer.ValueGenerator.GenerateUniqueId(UniqueIdType.Unit, Game.Instance.Player.GameId, identifier);
                return id;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating {type}", UniqueIdType.Unit);
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
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating {type}", UniqueIdType.Fact);
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
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating {type}", UniqueIdType.ChangeBlueprintUnit);
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
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating {type}", UniqueIdType.ItemEntity);
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
                Main.GetLogger<RandomIdGenerationPatches>().LogError(ex, "Error while generating {type}", UniqueIdType.AbilityData);
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
