using System;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Combat;
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
                Main.GetLogger<UnitCommandsPatches>().LogInformation("Starting unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}, StandardCD={StandardAction}, MoveCD={MoveAction}, SwiftCD={SwiftAction}, InitiativeCD={Initiative}",
                    __instance.Executor.UniqueId, __instance.Target.UniqueId, __instance.IsFullAttack(), cd.StandardAction, cd.MoveAction, cd.SwiftAction, cd.Initiative);

                OnUnitAttack(__instance, forceMount: false);
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError(ex, "Unable to handle UnitAttack");
                throw;
            }
        }

        /// <summary>
        /// UnitAttack command which did not result in actual attack, i.e. clicking unit to approach
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="result"></param>
        [HarmonyPatch(typeof(UnitCommand), nameof(UnitCommand.ForceFinishForTurnBased))]
        [HarmonyPrefix]
        public static void UnitCommand_ForceFinishForTurnBased_Prefix(UnitCommand __instance, ResultType result)
        {
            if (!Main.Multiplayer.IsActive || result != ResultType.Success || __instance.IsStarted || __instance is not UnitAttack unitAttack || __instance.Executor.SaddledPart != null)
            {
                return;
            }

            try
            {
                var cd = unitAttack.Executor.CombatState.Cooldown;
                Main.GetLogger<UnitCommandsPatches>().LogInformation("Sending forcefinished unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}, StandardCD={StandardAction}, MoveCD={MoveAction}, SwiftCD={SwiftAction}, InitiativeCD={Initiative}",
                    unitAttack.Executor.UniqueId, unitAttack.Target.UniqueId, unitAttack.IsFullAttack(), cd.StandardAction, cd.MoveAction, cd.SwiftAction, cd.Initiative);

                OnUnitAttack(unitAttack, forceMount: __instance.Executor.RiderPart != null);
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCommandsPatches>().LogError(ex, "Unable to handle UnitAttack ForceFinishForTurnBased");
                throw;
            }
        }

        [HarmonyPatch(typeof(UnitAttack), nameof(UnitAttack.InitAttacks))]
        [HarmonyPostfix]
        public static void UnitAttack_InitAttacks_Postfix(UnitAttack __instance)
        {
            if (!Main.Multiplayer.IsActive || !__instance.CreatedByPlayer)
            {
                return;
            }

            Main.GetLogger<UnitCommandsPatches>().LogInformation("Attacks have been initialized. AttackIndex={AttackIndex}, AttackCount={AttackCount}", __instance.m_AttackIndex, __instance.m_AllAttacks.Count);
        }

        [HarmonyPatch(typeof(UnitUseAbility), nameof(UnitUseAbility.OnStart))]
        [HarmonyPostfix]
        public static void UnitUseAbility_OnStart_Postfix(UnitUseAbility __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (__instance.Ability.StickyTouch != null)
            {
                Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of another usage. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", __instance.Executor.UniqueId, __instance.Ability.Name, __instance.Ability.UniqueId);
                return;
            }

            if (DoesMountMakeSameAction(__instance.Executor))
            {
                Main.GetLogger<UnitCommandsPatches>().LogWarning("Skipping ability use as it's a part of mounted combat unit command. UnitId={UnitId}, AbilityName={AbilityName}, AbilityId={AbilityId}", __instance.Executor.UniqueId, __instance.Ability.Name, __instance.Ability.UniqueId);
                return;
            }

            OnAbilityUse(__instance);
        }

        private static void OnAbilityUse(UnitUseAbility command)
        {
            var path = PathVisualizer.Instance.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => new NetworkVector3(v.x, v.y, v.z)).ToList();
            var networkAbility = new NetworkAbility
            {
                Id = command.Ability.UniqueId,
                Name = command.Ability.NameForAcronym,
                SpellbookId = command.Ability.Spellbook?.Blueprint.Name.Key,
                CasterId = command.Executor.UniqueId,
                TargetId = command.Target?.Unit?.UniqueId,
                TargetPoint = command.Target?.Point == null ? null : new NetworkVector3(command.Target.Point.x, command.Target.Point.y, command.Target.Point.z),
                VectorPath = networkPath,
                CommandType = command.Type.ToString(),
                ConvertedFromId = command.Ability.ConvertedFrom?.UniqueId
            };

            Main.Multiplayer.OnAbilityUse(networkAbility);
        }

        private static void OnUnitAttack(UnitAttack command, bool forceMount)
        {
            var path = PathVisualizer.Instance.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => new NetworkVector3(v.x, v.y, v.z)).ToList();
            var executor = forceMount ? command.Executor.RiderPart.SaddledUnit.UniqueId : command.Executor.UniqueId;
            var networkAbility = new NetworkUnitAttack
            {
                ExecutorUnitId = executor,
                TargetUnitId = command.TargetUnit?.UniqueId,
                IsFullAttack = command.IsAttackFull,
                VectorPath = networkPath
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
