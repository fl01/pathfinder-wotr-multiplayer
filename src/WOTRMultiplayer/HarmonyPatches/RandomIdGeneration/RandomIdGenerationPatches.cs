using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.View;
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

            //Main.GetLogger<Player>().LogInformation("New id generated: {id}", __result);
        }

        [HarmonyPatch(typeof(EntityCreationController), nameof(EntityCreationController.SpawnEntity))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityCreationController_SpawnEntity_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
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
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
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
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
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
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
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
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName ?? attr.info.methodType?.ToString()}";
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(RandomIdGenerationPatches), nameof(RandomIdGenerationPatches.GetNewItemEntityId));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor)).Advance(-1);
            if (match == null)
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

        public static string GetNewEntityUniqueId(EntityViewBase prefab)
        {
            return Game.Instance.Player.GetNewUniqueId();
        }

        public static string GetNewUnitUniqueId(BlueprintUnit unit, UnitEntityView prefab)
        {
            var identifier = $"{GetCommonIdPart()}:{prefab.name}";
            var id = Main.Multiplayer.IdGenerator.GenerateId(UniqueIdType.Unit, Game.Instance.Player.GameId, identifier);
            return id;
        }

        public static string GetNewEntityFactUniqueId(EntityFact fact)
        {
            var identifier = $"{GetCommonIdPart()}:{fact.GetType().Name}:{fact.NameForAcronym}:{fact.Owner?.UniqueId}";
            var id = Main.Multiplayer.IdGenerator.GenerateId(UniqueIdType.Fact, Game.Instance.Player.GameId, identifier);
            return id;
        }

        public static string GetNewChangeBlueprintUniqueId(UnitEntityData unitEntityData, BlueprintUnit blueprintUnit)
        {
            var identifier = $"{GetCommonIdPart()}:{unitEntityData.CharacterName}";
            var id = Main.Multiplayer.IdGenerator.GenerateId(UniqueIdType.ChangeBlueprintUnit, Game.Instance.Player.GameId, identifier);
            return id;
        }

        public static string GetNewItemEntityId(BlueprintItem blueprintItem)
        {
            var identifier = $"{GetCommonIdPart()}:{blueprintItem.NameForAcronym}:{blueprintItem.ItemType}:{blueprintItem.MiscellaneousType}";
            var id = Main.Multiplayer.IdGenerator.GenerateId(UniqueIdType.ItemEntity, Game.Instance.Player.GameId, identifier);
            return id;
        }

        private static string GetCommonIdPart()
        {
            var area = Game.Instance.CurrentlyLoadedArea?.name ?? "no-area";
            var gameId = Game.Instance.Player?.GameId;
            return $"{area}:{gameId}";
        }
    }
}
