using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.View.MapObjects.Traps;
using Kingmaker.View.MapObjects.Traps.Simple;
using Kingmaker.View.MapObjects.Traps.Simple.Strategies;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Services.Random;
using static Kingmaker.Blueprints.BlueprintTrapSettings;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class TrapObjectDataPatches
    {
        [HarmonyPatch(typeof(TrapObjectData), nameof(TrapObjectData.Interact))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TrapObjectData_Interact_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(TrapObjectDataPatches), nameof(TrapObjectDataPatches.OnTrapDisarmRolled));
            var lookFor = AccessTools.Method(typeof(GameHelper), nameof(GameHelper.TriggerSkillCheck));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<TrapObjectDataPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, extraCall),
            };
            match = match.Advance(2).Insert(newInstructions);
            Main.GetLogger<TrapObjectDataPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(SimpleTrapCastSpell), nameof(SimpleTrapCastSpell.ApplyAbilityToTargets))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SimpleTrapCastSpell_ApplyAbilityToTargets_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(TrapObjectDataPatches), nameof(TrapObjectDataPatches.OnApplyTrapAbility));
            var lookFor = AccessTools.Method(typeof(IntRange), nameof(IntRange.PickRandom));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<TrapObjectDataPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, extraCall),
            };
            match = match.Advance(-2).RemoveInstructions(3).Insert(newInstructions);
            Main.GetLogger<TrapObjectDataPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(SimpleTrapObjectView), nameof(SimpleTrapObjectView.CreateData))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SimpleTrapObjectView_CreateData_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            var rollDisableDCCall = AccessTools.Method(typeof(TrapObjectDataPatches), nameof(TrapObjectDataPatches.OnRollTrapDisableDC));
            var lookForDisableDC = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var match = matcher.SearchForward(x => x.Calls(lookForDisableDC));

            if (match.IsInvalid)
            {
                Main.GetLogger<TrapObjectDataPatches>().LogError("Invalid transpiler position (DisableDC). Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }
            var disableDCInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_1),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, rollDisableDCCall),
            };
            match = match.Advance(-6).RemoveInstructions(7).Insert(disableDCInstructions);

            var lookForPerception = AccessTools.Method(typeof(IntRange), nameof(IntRange.PickRandom));
            match = matcher.SearchForward(x => x.Calls(lookForPerception));

            if (match.IsInvalid)
            {
                Main.GetLogger<TrapObjectDataPatches>().LogError("Invalid transpiler position (RollPerception). Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }
            var rollPerceptionCall = AccessTools.Method(typeof(TrapObjectDataPatches), nameof(TrapObjectDataPatches.OnRollTrapPerceptionDC));
            var perceptionInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_1),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, rollPerceptionCall),
            };
            match = match.Advance(-2).RemoveInstructions(3).Insert(perceptionInstructions);
            Main.GetLogger<TrapObjectDataPatches>().LogInformation("Transpiler has been applied (DisableDC + RollPerception). Target={Target}", target);
            return matcher.Instructions();
        }

        private static int OnRollTrapDisableDC(BlueprintTrapSettings blueprintTrap, SimpleTrapObjectView simpleTrapObjectView)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(blueprintTrap.DisableDC.from, blueprintTrap.DisableDC.to);
            }

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSaveSeed = Main.Multiplayer.GetLoadedSaveSeed();

                var identifier = $"{nameof(SimpleTrapObjectView)}:{nameof(OnRollTrapDisableDC)}:{blueprintTrap.name}:{blueprintTrap.AssetGuid}:{blueprintTrap.DisableDC.from}:{blueprintTrap.DisableDC.to}_{sessionSeed}:{loadedSaveSeed}";
                var disableDC = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, identifier, blueprintTrap.DisableDC.from, blueprintTrap.DisableDC.to);
                Main.GetLogger<TrapObjectDataPatches>().LogInformation("Trap disable dc has been rolled. TrapId={TrapId}, Result={Result}, Identifier={Identifier}", simpleTrapObjectView.UniqueId, disableDC, identifier);
                return disableDC;
            }
            catch (Exception ex)
            {
                Main.GetLogger<TrapObjectDataPatches>().LogError(ex, "Error while rolling trap disable dc. TrapId={TrapId}", simpleTrapObjectView.UniqueId);
                throw;
            }
        }

        private static int OnRollTrapPerceptionDC(BlueprintTrapSettings blueprintTrap, SimpleTrapObjectView simpleTrapObjectView)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return blueprintTrap.PerceptionDC.PickRandom();
            }

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSaveSeed = Main.Multiplayer.GetLoadedSaveSeed();

                var identifier = $"{nameof(SimpleTrapObjectView)}:{nameof(OnRollTrapPerceptionDC)}:{blueprintTrap.name}:{blueprintTrap.AssetGuid}:{blueprintTrap.PerceptionDC.from}:{blueprintTrap.PerceptionDC.to}_{sessionSeed}:{loadedSaveSeed}";
                var perceptionDC = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, identifier, blueprintTrap.PerceptionDC.from, blueprintTrap.PerceptionDC.to);
                Main.GetLogger<TrapObjectDataPatches>().LogInformation("Trap perception dc has been rolled. TrapId={TrapId}, Result={Result}, Identifier={Identifier}", simpleTrapObjectView.UniqueId, perceptionDC, identifier);
                return perceptionDC;
            }
            catch (Exception ex)
            {
                Main.GetLogger<TrapObjectDataPatches>().LogError(ex, "Error while rolling trap perception dc. TrapId={TrapId}", simpleTrapObjectView.UniqueId);
                throw;
            }
        }

        private static int OnApplyTrapAbility(Ability ability, BlueprintTrapSettings blueprintTrap)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return blueprintTrap.ActorStatMod.PickRandom();
            }

            var trapId = ability.Owner?.Unit?.UniqueId;

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSaveSeed = Main.Multiplayer.GetLoadedSaveSeed();
                var areaSeed = Main.Multiplayer.GetAreaSeed();
                var combatSeed = Main.Multiplayer.GetCombatSeed();
                var combatTurnSeed = Main.Multiplayer.GetCombatTurnSeed();

                var identifier = $"{nameof(SimpleTrapCastSpell)}:{nameof(OnApplyTrapAbility)}:{trapId}:{ability.NameForAcronym}:{blueprintTrap.name}:{blueprintTrap.AssetGuid}:{blueprintTrap.ActorStatMod.from}:{blueprintTrap.ActorStatMod.to}_{sessionSeed}:{loadedSaveSeed}:{areaSeed}:{combatSeed}:{combatTurnSeed}";
                var casterModifer = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, identifier, blueprintTrap.ActorStatMod.from, blueprintTrap.ActorStatMod.to);
                Main.GetLogger<TrapObjectDataPatches>().LogInformation("Trap caster modifier has been rolled. TrapId={TrapId}, AbilityName={AbilityName}, Result={Result}, Identifier={Identifier}",
                    trapId, ability.NameForAcronym, casterModifer, identifier);
                return casterModifer;
            }
            catch (Exception ex)
            {
                Main.GetLogger<TrapObjectDataPatches>().LogError(ex, "Error while rolling trap ability caster modifier. TrapId={TrapId}, AbilityName={AbilityName}", trapId, ability.NameForAcronym);
                throw;
            }
        }

        private static void OnTrapDisarmRolled(TrapObjectData trapObjectData, UnitEntityData unit, RuleSkillCheck ruleSkillCheck)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var trapDisarm = new NetworkTrapDisarm
            {
                IsSuccess = ruleSkillCheck.Success,
                MapObject = Main.Mapper.Map<NetworkMapObject>(trapObjectData),
                Roll = ruleSkillCheck.RollResult,
                UnitId = unit.UniqueId
            };

            Main.Multiplayer.OnTrapDisarmRolled(trapDisarm);
        }
    }
}
