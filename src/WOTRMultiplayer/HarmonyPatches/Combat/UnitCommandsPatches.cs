using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Class.Kineticist;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Parts;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Random;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitCommandsPatches
    {
        private readonly static AsyncLocal<bool> _isSecondGetUpCommand = new();

        [HarmonyPatch(typeof(UnitAttack), nameof(UnitAttack.UpdateTarget))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitAttack_UpdateTarget_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(UnitCommandsPatches), nameof(UnitCommandsPatches.SelectNextTarget));
            var lookFor = AccessTools.Method(typeof(UnitCommand), nameof(UnitCommand.CommandTargetUntargetable));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-15).Insert(newInstructions);
            Main.GetLogger<UnitCommandsPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        /// <summary>
        /// There is a need to get consistency in the next target selection in case of rider/mount units.
        /// Multiattack (next attack after unit is dead) should hit the same target across every MP players, so the idea is to enforce rider/mount order position (rolled deterministically)
        /// </summary>
        /// <param name="nearbyUnits"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        private static List<UnitEntityData> SelectNextTarget(List<UnitEntityData> nearbyUnits, UnitAttack command)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return nearbyUnits;
            }

            var count = nearbyUnits.Count;
            var sortedUnits = new UnitEntityData[count];
            var positionIsFilled = new bool[count];

            var indexMap = new Dictionary<UnitEntityData, int>(count);
            for (int i = 0; i < count; i++)
            {
                indexMap[nearbyUnits[i]] = i;
            }

            for (int i = 0; i < count; i++)
            {
                if (positionIsFilled[i])
                {
                    continue;
                }

                var unit = nearbyUnits[i];

                // untargetable / unconscious
                if (IsExcludedUnit(unit, command.Executor))
                {
                    sortedUnits[i] = unit;
                    positionIsFilled[i] = true;
                    continue;
                }

                UnitEntityData pairedUnit = null;

                if (unit.RiderPart?.SaddledUnit != null && !IsExcludedUnit(unit.RiderPart.SaddledUnit, command.Executor))
                {
                    pairedUnit = unit.RiderPart.SaddledUnit;
                }
                else if (unit.SaddledPart?.Rider != null && !IsExcludedUnit(unit.SaddledPart.Rider, command.Executor))
                {
                    pairedUnit = unit.SaddledPart.Rider;
                }

                if (pairedUnit != null && indexMap.TryGetValue(pairedUnit, out var otherIndex))
                {
                    var first = Math.Min(i, otherIndex);
                    var second = Math.Max(i, otherIndex);

                    bool riderFirst = ShouldRiderBeFirst(command.Executor);

                    sortedUnits[first] = riderFirst ? pairedUnit : unit;
                    sortedUnits[second] = riderFirst ? unit : pairedUnit;

                    positionIsFilled[first] = true;
                    positionIsFilled[second] = true;
                }
                else
                {
                    sortedUnits[i] = unit;
                    positionIsFilled[i] = true;
                }
            }

            return [.. sortedUnits];
        }

        private static bool IsExcludedUnit(UnitEntityData unitEntityData, UnitEntityData executor)
        {
            return unitEntityData.Descriptor.State.IsConscious || UnitCommand.CommandTargetUntargetable(executor, unitEntityData, null);
        }

        private static bool ShouldRiderBeFirst(UnitEntityData executor)
        {
            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSaveSeed = Main.Multiplayer.GetLoadedSaveSeed();
                var combatSeed = Main.Multiplayer.GetCombatSeed();
                var combatTurnSeed = Main.Multiplayer.GetCombatTurnSeed();
                var armyCombatSeed = Main.Multiplayer.GetCrusadeArmyCombatAreaSeed();

                var identifier = $"{nameof(UnitAttack)}:{nameof(SelectNextTarget)}:{executor.UniqueId}:{sessionSeed}:{loadedSaveSeed}:{combatSeed}:{combatTurnSeed}:{armyCombatSeed}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(IdentifierLifetime.CombatTurn, identifier);
                var riderShouldBeFirst = random.Next(0, 2) == 1;
                Main.GetLogger<UnitCommandsPatches>().LogInformation("Rider/Mount order has been rolled. Executor={Executor}, Result={Result}, Identifier={Identifier}", executor.UniqueId, riderShouldBeFirst, identifier);
                return riderShouldBeFirst;
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError(ex, "Unable to roll rider/mount order. Executor={Executor}", executor.UniqueId);
                throw;
            }
        }


        [HarmonyPatch(typeof(UnitCommand), nameof(UnitCommand.OnRun))]
        [HarmonyPrefix]
        public static void UnitCommand_OnRun_Prefix(UnitCommand __instance)
        {
            if (!Main.Multiplayer.IsActive || TurnControllerPatches.IsSimulation.Value || TacticalCombatHelper.IsActive)
            {
                return;
            }

            try
            {
                var isGetUp = Game.Instance.TurnBasedCombatController.CurrentTurn?.UnitCanGetUpOnCommand?.Value ?? false;
                switch (__instance)
                {
                    case UnitInteractWithUnit unitInteractWithUnit when unitInteractWithUnit.CreatedByPlayer:
                        var networkUnitInteractWithUnit = new NetworkUnitInteractWithUnit
                        {
                            InitiatorUnitId = __instance.Executor.UniqueId,
                            TargetUnitId = unitInteractWithUnit.TargetUnit.UniqueId
                        };
                        Main.Multiplayer.OnUnitInteractWithUnit(networkUnitInteractWithUnit);
                        break;
                    case UnitLootUnit unitLootUnit when unitLootUnit.CreatedByPlayer:
                        var path = PathVisualizer.Instance?.CurrentPathForUnit(unitLootUnit.Executor.View);
                        var networkPath = path?.vectorPath.Select(v => v.ToNetworkVector3()).ToList();
                        var movementLimit = Game.Instance.TurnBasedCombatController.CurrentTurn?.CurrentMovementLimit;
                        var networkUnitLootUnit = new NetworkUnitLootUnit
                        {
                            InitiatorUnitId = __instance.Executor.UniqueId,
                            TargetUnitId = unitLootUnit.TargetUnit.UniqueId,
                            VectorPath = networkPath,
                            MovementLimit = movementLimit?.ToString(),
                        };
                        Main.Multiplayer.OnUnitLootUnit(networkUnitLootUnit);
                        break;
                    // attack/ability commands can be synced immediately if combat has not started
                    case UnitAttack unitAttack when !Game.Instance.Player.IsInCombat || isGetUp:
                        // for some reason game runs x2 attack commands in this case, but second one must be suppressed to not cause an attack for other players
                        if (isGetUp && _isSecondGetUpCommand.Value)
                        {
                            Main.GetLogger<UnitCommandsPatches>().LogWarning("Doubled GetUp attack command has been ignored. UnitId={UnitId}", __instance.Executor.UniqueId);
                            _isSecondGetUpCommand.Value = false;
                            return;
                        }

                        OnUnitAttack(unitAttack);
                        if (isGetUp)
                        {
                            _isSecondGetUpCommand.Value = true;
                        }
                        break;
                    case UnitUseAbility unitUseAbility when !Game.Instance.Player.IsInCombat:
                        if (ShouldSkipNotification(unitUseAbility))
                        {
                            return;
                        }
                        OnAbilityUse(unitUseAbility);
                        break;
                }
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError(ex, "Unable to handle command start. CommandType={CommandType}", __instance.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// Attack has been started
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(typeof(UnitAttack), nameof(UnitAttack.OnStart))]
        [HarmonyPostfix]
        public static void UnitAttack_OnStart_Postfix(UnitAttack __instance)
        {
            if (!Main.Multiplayer.IsActive || (!Game.Instance.Player.IsInCombat && !TacticalCombatHelper.IsActive))
            {
                return;
            }

            try
            {
                if (__instance.IsCharge)
                {
                    Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping attack command in combat as it's a part of charge ability. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}", __instance.Executor.UniqueId, __instance.Target.UniqueId);
                    return;
                }

                var turnTouchAbility = Game.Instance.TurnBasedCombatController?.m_CurrentTurn?.TouchAbility;
                if (turnTouchAbility != null)
                {
                    var unitPartMagus = __instance.Executor.Get<UnitPartMagus>();
                    var unitPartTouch = __instance.Executor.Get<UnitPartTouch>();
                    var actionStates = Game.Instance.TurnBasedCombatController.CurrentTurn.GetActionsStates(__instance.Executor);
                    if (unitPartMagus != null
                        && __instance.TargetUnit != null
                        && !unitPartMagus.EldritchArcher
                        && unitPartTouch != null
                        && unitPartTouch.Ability.Data == turnTouchAbility.Data
                        && unitPartMagus.Spellstrike.Active
                        && unitPartMagus.IsSpellFromMagusSpellList(unitPartTouch.Ability.Data)
                        && __instance.Executor.IsEnemy(__instance.TargetUnit)
                        && __instance.Executor.GetThreatHand() != null
                        && actionStates != null
                            && (actionStates.Standard.CurrentAbility is AbilityData standardAbility && standardAbility == turnTouchAbility.StickyTouch
                                || actionStates.Swift.CurrentAbility is AbilityData swiftAbility && swiftAbility == turnTouchAbility.StickyTouch))
                    {
                        Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping attack command use as it's a part of magus combat. UnitId={UnitId}, TargetUnitId={TargetUnitId}", __instance.Executor.UniqueId, __instance.Target.UniqueId);
                        return;
                    }
                }

                Main.GetLogger<UnitCommandsPatches>().LogWarning("Starting unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}, ShouldUnitApproach={ShouldUnitApproach}, Limit={limit}, IsIgnoreCooldown={IsIgnoreCooldown}", __instance.Executor.UniqueId, __instance.Target.UniqueId, __instance.IsFullAttack(), __instance.ShouldUnitApproach, __instance.IsIgnoreCooldown);
                OnUnitAttack(__instance);
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError(ex, "Unable to handle UnitAttack");
                throw;
            }
        }

        /// <summary>
        /// UnitAttack/Ability command which did not result in actual attack/spell use, i.e. clicking unit to approach
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="result"></param>
        [HarmonyPatch(typeof(UnitCommand), nameof(UnitCommand.ForceFinishForTurnBased))]
        [HarmonyPrefix]
        public static void UnitCommand_ForceFinishForTurnBased_Prefix(UnitCommand __instance, ResultType result)
        {
            if (!Main.Multiplayer.IsActive
                || result != ResultType.Success
                || __instance.IsStarted
                || (!Game.Instance.Player.IsInCombat && !TacticalCombatHelper.IsActive))
            {
                return;
            }

            try
            {
                switch (__instance)
                {
                    case UnitAttack unitAttack:
                        Main.GetLogger<UnitCommandsPatches>().LogInformation("Forcefinished unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}", unitAttack.Executor.UniqueId, unitAttack.Target.UniqueId, unitAttack.IsFullAttack());
                        OnUnitMove(unitAttack.Executor.UniqueId, __instance.Executor.Position);
                        break;
                    case UnitUseAbility unitUseAbility:
                        Main.GetLogger<UnitCommandsPatches>().LogInformation("Forcefinished unit useability command. ExecutorUnitId={ExecutorUnitId}", __instance.Executor.UniqueId, unitUseAbility.TargetUnit?.UniqueId);
                        OnUnitMove(unitUseAbility.Executor.UniqueId, __instance.Executor.Position);
                        break;
                }
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError(ex, "Unable to handle ForceFinishForTurnBased. CommandType={CommandType}", __instance.GetType().Name);
                throw;
            }
        }

        [HarmonyPatch(typeof(UnitUseAbility), nameof(UnitUseAbility.OnStart))]
        [HarmonyPostfix]
        public static void UnitUseAbility_OnStart_Postfix(UnitUseAbility __instance)
        {
            if (!Main.Multiplayer.IsActive || (!Game.Instance.Player.IsInCombat && !TacticalCombatHelper.IsActive))
            {
                return;
            }

            try
            {
                if (ShouldSkipNotification(__instance))
                {
                    return;
                }

                OnAbilityUse(__instance);
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError(ex, "Unable to handle unit ability use command. UnitId={UnitId}, AbilityName={AbilityName}", __instance.Executor.UniqueId, __instance.Ability.NameForAcronym);
                throw;
            }
        }

        private static bool ShouldSkipNotification(UnitUseAbility command)
        {
            if (ShouldIgnoreStickyTouchAbilityCast(command))
            {
                Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of sticky touch delivery usage. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", command.Executor.UniqueId, command.Ability.Name, command.Ability.UniqueId);
                return true;
            }

            if (DoesRiderMakeSameAction(command.Executor))
            {
                Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of mounted combat unit command. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", command.Executor.UniqueId, command.Ability.Name, command.Ability.UniqueId);
                return true;
            }

            if (IsKineticistAutousedAbility(command))
            {
                Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of kineticist autouse. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", command.Executor.UniqueId, command.Ability.Name, command.Ability.UniqueId);
                return true;
            }

            if (IsAlchemistFastBombsSequentialCast(command))
            {
                Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of alchemist sequential fast bombs usage. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", command.Executor.UniqueId, command.Ability.Name, command.Ability.UniqueId);
                return true;
            }

            // Tactical combat - TotalDefense
            if (string.Equals(command.Ability.Blueprint.AssetGuid.ToString(), "5fcc24b820f55104892097782b92228e", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool IsAlchemistFastBombsSequentialCast(UnitUseAbility command)
        {
            if (command.Executor.State?.FastBombsFeature == null || Game.Instance.TurnBasedCombatController.CurrentTurn == null)
            {
                return false;
            }

            var isFirstCast = command.Executor.State.FastBombsFeature.Abilities.Any(a => a.Guid == command.Ability.Blueprint.AssetGuid)
                && Game.Instance.TurnBasedCombatController.CurrentTurn.CurrentAbility == command.Ability; // CurrentAbility == command.Ability for a first actual usage

            return !isFirstCast;
        }

        private static bool ShouldIgnoreStickyTouchAbilityCast(UnitUseAbility command)
        {
            if (command.Ability.StickyTouch == null)
            {
                return false;
            }

            // There are two scenarios for sticky touch abilities:
            // 1. Prepared cast – the touch ability was created earlier but not yet delivered.
            //    This results in a single 'useability touch' command.
            // 2. Unprepared cast – a sequence of two commands:
            //    useability -> (move towards target) -> 'useability touch'.
            // The second command in this sequence must be ignored,
            // since it is processed internally by the game and should not be sent to other players.
            var touchPart = command.Executor.Get<UnitPartTouch>();
            var shouldIgnore = touchPart != null && touchPart.AutoCastCommand == command;
            return shouldIgnore;
        }

        private static bool IsKineticistAutousedAbility(UnitUseAbility instance)
        {
            var kineticistPart = instance.Executor.Get<UnitPartKineticist>();
            var shouldSkip = kineticistPart != null && kineticistPart.GatherPowerAbility?.Data == instance.Ability;
            return shouldSkip;
        }

        private static void OnAbilityUse(UnitUseAbility command)
        {
            var path = PathVisualizer.Instance?.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => v.ToNetworkVector3()).ToList();
            var movementLimit = Game.Instance.TurnBasedCombatController.CurrentTurn?.CurrentMovementLimit;
            var attackMode = Game.Instance.TurnBasedCombatController.CurrentTurn?.m_AttackMode;

            var abilityUse = new NetworkAbilityUse
            {
                Ability = Main.Mapper.Map<NetworkAbility>(command.Ability),
                Target = Main.Mapper.Map<NetworkTargetWrapper>(command.Target),
                InitiatorUnitId = command.Executor.UniqueId,
                VectorPath = networkPath,
                CommandType = command.Type.ToString(),
                MovementLimit = movementLimit?.ToString(),
                AttackMode = attackMode?.ToString()
            };

            Main.Multiplayer.OnAbilityUse(abilityUse);
        }

        private static void OnUnitMove(string unitId, Vector3 destination)
        {
            var movementLimit = Game.Instance.TurnBasedCombatController.CurrentTurn?.CurrentMovementLimit;
            var path = PathVisualizer.Instance?.m_CurrentPath?.vectorPath;
            var unitMoveTo = new NetworkUnitMoveTo
            {
                InitiatorUnitId = unitId,
                VectorPath = [.. path?.Select(x => x.ToNetworkVector3()) ?? []],
                Destination = destination.ToNetworkVector3(),
                MovementLimit = movementLimit?.ToString()
            };

            Main.Multiplayer.OnUnitMoveTo(unitMoveTo);
        }

        private static void OnUnitAttack(UnitAttack command)
        {
            var path = PathVisualizer.Instance?.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => v.ToNetworkVector3()).ToList();
            var movementLimit = Game.Instance.TurnBasedCombatController.CurrentTurn?.CurrentMovementLimit;

            var unitAttack = new NetworkUnitAttack
            {
                InitiatorUnitId = command.Executor.UniqueId,
                TargetUnitId = command.TargetUnit?.UniqueId,
                IsFullAttack = command.IsAttackFull,
                IsSingleAttack = command.IsSingleAttack,
                IsCharge = command.IsCharge,
                VectorPath = networkPath,
                MovementLimit = movementLimit?.ToString()
            };

            Main.Multiplayer.OnUnitAttackCommandStarted(unitAttack);
        }

        private static bool DoesMountMakeSameAction(UnitEntityData unitEntity)
        {
            if (unitEntity.RiderPart == null)
            {
                return false;
            }

            var mount = unitEntity.RiderPart.SaddledUnit;
            return mount.Commands.Attack != null && unitEntity.Commands.Attack != null || mount.Commands.UnitUseAbility != null && unitEntity.Commands.UnitUseAbility != null;
        }

        private static bool DoesRiderMakeSameAction(UnitEntityData unitEntity)
        {
            if (unitEntity.SaddledPart == null)
            {
                return false;
            }

            var rider = unitEntity.SaddledPart.Rider;
            return rider.Commands.Attack != null && unitEntity.Commands.Attack != null || rider.Commands.UnitUseAbility != null && unitEntity.Commands.UnitUseAbility != null;
        }
    }
}
