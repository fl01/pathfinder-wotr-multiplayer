using System;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Extensions;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    /// <summary>
    /// Mounted combat difference - game creates x2 commands (one for rider, one for mount). Rider commands should not be synced since they are created by game automatically
    /// </summary>
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

                if (DoesMountMakeSameAction(__instance.Executor))
                {
                    Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping attack command use as it's a part of mounted combat unit command. UnitId={UnitId}, TargetUnitId={TargetUnitId}", __instance.Executor.UniqueId, __instance.Target.UniqueId);
                    return;
                }

                var cd = __instance.Executor.CombatState.Cooldown;
                Main.GetLogger<UnitCommandsPatches>().LogWarning("Starting unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}, ShouldUnitApproach={ShouldUnitApproach}, Limit={limit}, IsIgnoreCooldown={IsIgnoreCooldown}, StandardCD={StandardAction}, MoveCD={MoveAction}, SwiftCD={SwiftAction}, InitiativeCD={Initiative}",
                    __instance.Executor.UniqueId, __instance.Target.UniqueId, __instance.IsFullAttack(), __instance.ShouldUnitApproach, __instance.IsIgnoreCooldown, cd.StandardAction, cd.MoveAction, cd.SwiftAction, cd.Initiative);

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
            if (!Main.Multiplayer.IsActive || result != ResultType.Success || __instance.IsStarted || __instance.Executor.SaddledPart != null)
            {
                return;
            }

            try
            {
                var cd = __instance.Executor.CombatState.Cooldown;
                switch (__instance)
                {
                    case UnitAttack unitAttack:
                        Main.GetLogger<UnitCommandsPatches>().LogInformation("Forcefinished unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}, StandardCD={StandardAction}, MoveCD={MoveAction}, SwiftCD={SwiftAction}, InitiativeCD={Initiative}",
                            unitAttack.Executor.UniqueId, unitAttack.Target.UniqueId, unitAttack.IsFullAttack(), cd.StandardAction, cd.MoveAction, cd.SwiftAction, cd.Initiative);
                        OnUnitAttack(unitAttack, forceMount: __instance.Executor.RiderPart != null);
                        break;
                    case UnitUseAbility unitUseAbility:
                        Main.GetLogger<UnitCommandsPatches>().LogInformation("Forcefinished unit useability command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, StandardCD={StandardAction}, MoveCD={MoveAction}, SwiftCD={SwiftAction}, InitiativeCD={Initiative}",
                            __instance.Executor.UniqueId, unitUseAbility.TargetUnit?.UniqueId, cd.StandardAction, cd.MoveAction, cd.SwiftAction, cd.Initiative);
                        OnAbilityUse(unitUseAbility);
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
                if (__instance.Ability.StickyTouch != null)
                {
                    Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of sticky touch usage. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", __instance.Executor.UniqueId, __instance.Ability.Name, __instance.Ability.UniqueId);
                    return;
                }

                if (DoesMountMakeSameAction(__instance.Executor))
                {
                    Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of mounted combat unit command. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", __instance.Executor.UniqueId, __instance.Ability.Name, __instance.Ability.UniqueId);
                    return;
                }

                OnAbilityUse(__instance);
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError(ex, "Unable to handle unit ability use command");
                throw;
            }
        }

        private static void OnAbilityUse(UnitUseAbility command)
        {
            var path = PathVisualizer.Instance?.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => v.ToNetworkVector3()).ToList();
            var networkAbility = new NetworkAbility
            {
                Id = command.Ability.UniqueId,
                Name = command.Ability.NameForAcronym,
                SpellbookId = command.Ability.Spellbook?.Blueprint.Name.Key,
                CasterId = command.Executor.UniqueId,
                VectorPath = networkPath,
                Target = new NetworkTargetWrapper(
                    command.Target.Point.ToNetworkVector3(),
                    command.Target.Orientation,
                    command.Target.Unit?.UniqueId),
                CommandType = command.Type.ToString(),
                ConvertedFromId = command.Ability.ConvertedFrom?.UniqueId
            };

            Main.Multiplayer.OnAbilityUse(networkAbility);
        }

        private static void OnUnitAttack(UnitAttack command, bool forceMount)
        {
            var path = PathVisualizer.Instance?.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => v.ToNetworkVector3()).ToList();
            var executor = forceMount ? command.Executor.RiderPart.SaddledUnit.UniqueId : command.Executor.UniqueId;
            var movementLimit = Game.Instance.TurnBasedCombatController.CurrentTurn?.CurrentMovementLimit;

            var networkAbility = new NetworkUnitAttack
            {
                ExecutorUnitId = executor,
                TargetUnitId = command.TargetUnit?.UniqueId,
                IsFullAttack = command.IsAttackFull,
                IsSingleAttack = command.IsSingleAttack,
                VectorPath = networkPath,
                MovementLimit = movementLimit?.ToString()
            };

            Main.Multiplayer.OnUnitAttackCommandStarted(networkAbility);
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
    }
}
