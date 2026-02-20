using System;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.Kineticist;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Parts;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Extensions;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitCommandsPatches
    {
        /// <summary>
        /// Attack has been started
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(typeof(UnitAttack), nameof(UnitAttack.OnStart))]
        [HarmonyPostfix]
        public static void UnitAttack_OnStart_Postfix(UnitAttack __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            try
            {
                if (Game.Instance.Player.IsInCombat && __instance.IsCharge)
                {
                    Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping attack command in combat as it's a part of charge ability. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}", __instance.Executor.UniqueId, __instance.Target.UniqueId);
                    return;
                }

                var turnTouchAbility = Game.Instance.TurnBasedCombatController?.m_CurrentTurn?.TouchAbility;
                if (turnTouchAbility != null)
                {
                    var unitPartMagus = __instance.Executor.Get<UnitPartMagus>();
                    var unitPartTouch = __instance.Executor.Get<UnitPartTouch>();
                    if (unitPartMagus != null
                        && __instance.TargetUnit != null
                        && !unitPartMagus.EldritchArcher
                        && unitPartTouch != null
                        && unitPartTouch.Ability.Data == turnTouchAbility.Data
                        && unitPartMagus.Spellstrike.Active
                        && unitPartMagus.IsSpellFromMagusSpellList(unitPartTouch.Ability.Data)
                        && __instance.Executor.IsEnemy(__instance.TargetUnit)
                        && __instance.Executor.GetThreatHand() != null)
                    {
                        Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping attack command use as it's a part of magus combat. UnitId={UnitId}, TargetUnitId={TargetUnitId}", __instance.Executor.UniqueId, __instance.Target.UniqueId);
                        return;
                    }
                }

                Main.GetLogger<UnitCommandsPatches>().LogWarning("Starting unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}, ShouldUnitApproach={ShouldUnitApproach}, Limit={limit}, IsIgnoreCooldown={IsIgnoreCooldown}", __instance.Executor.UniqueId, __instance.Target.UniqueId, __instance.IsFullAttack(), __instance.ShouldUnitApproach, __instance.IsIgnoreCooldown);
                OnUnitAttack(__instance, forceMount: false);
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
            if (!Main.Multiplayer.IsActive || result != ResultType.Success || __instance.IsStarted)
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
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            try
            {
                if (ShouldIgnoreStickyTouchAbilityCast(__instance))
                {
                    Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of sticky touch delivery usage. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", __instance.Executor.UniqueId, __instance.Ability.Name, __instance.Ability.UniqueId);
                    return;
                }

                if (DoesRiderMakeSameAction(__instance.Executor))
                {
                    Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of mounted combat unit command. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", __instance.Executor.UniqueId, __instance.Ability.Name, __instance.Ability.UniqueId);
                    return;
                }

                if (IsKineticistAutousedAbility(__instance))
                {
                    Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of kineticist autouse. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", __instance.Executor.UniqueId, __instance.Ability.Name, __instance.Ability.UniqueId);
                    return;
                }

                // Tactical combat - TotalDefense
                if (string.Equals(__instance.Ability.Blueprint.AssetGuid.ToString(), "5fcc24b820f55104892097782b92228e", StringComparison.OrdinalIgnoreCase))
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

        private static bool ShouldIgnoreStickyTouchAbilityCast(UnitUseAbility command)
        {
            if (command.Ability.StickyTouch == null)
            {
                return false;
            }

            if (Game.Instance.TurnBasedCombatController.CurrentTurn == null)
            {
                return true;
            }

            var actionStates = Game.Instance.TurnBasedCombatController.CurrentTurn.GetActionsStates(command.Executor);
            var isIgnored = command.Type switch
            {
                CommandType.Standard => !actionStates.Standard.CanUseAbility,
                CommandType.Swift => !actionStates.Swift.CanUseAbility,
                _ => false,
            };

            return isIgnored;
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
            var abilityUse = new NetworkAbilityUse
            {
                Ability = Main.Mapper.Map<NetworkAbility>(command.Ability),
                Target = Main.Mapper.Map<NetworkTargetWrapper>(command.Target),
                InitiatorUnitId = command.Executor.UniqueId,
                VectorPath = networkPath,
                CommandType = command.Type.ToString(),
                MovementLimit = movementLimit?.ToString()
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

        private static void OnUnitAttack(UnitAttack command, bool forceMount)
        {
            var path = PathVisualizer.Instance?.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => v.ToNetworkVector3()).ToList();
            var executor = forceMount ? command.Executor.RiderPart.SaddledUnit.UniqueId : command.Executor.UniqueId;
            var movementLimit = Game.Instance.TurnBasedCombatController.CurrentTurn?.CurrentMovementLimit;

            var unitAttack = new NetworkUnitAttack
            {
                InitiatorUnitId = executor,
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
