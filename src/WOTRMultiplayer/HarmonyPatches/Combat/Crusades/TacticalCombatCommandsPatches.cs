using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Armies.TacticalCombat.Commands;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Microsoft.Extensions.Logging;
using Pathfinding;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatCommandsPatches
    {
        [HarmonyPatch(typeof(TacticalCombatHelper), nameof(TacticalCombatHelper.CreateMoveCommand))]
        [HarmonyPostfix]
        public static void TacticalCombatHelper_CreateMoveCommand_Postfix(UnitEntityData unit, ref UnitCommand __result)
        {
            if (!Main.Multiplayer.IsActive || !unit.IsDirectlyControllable)
            {
                return;
            }

            OnTacticalCommand(unit, __result);
        }

        [HarmonyPatch(typeof(TacticalCombatHelper), nameof(TacticalCombatHelper.CreateAttackCommand))]
        [HarmonyPostfix]
        public static void TacticalCombatHelper_CreateAttackCommand_Postfix(UnitEntityData unit, ref UnitCommand __result)
        {
            if (!Main.Multiplayer.IsActive || !unit.IsDirectlyControllable)
            {
                return;
            }

            OnTacticalCommand(unit, __result);
        }

        [HarmonyPatch(typeof(TacticalCombatHelper), nameof(TacticalCombatHelper.CreateUseAbilityCommand))]
        [HarmonyPostfix]
        public static void TacticalCombatHelper_CreateUseAbilityCommand_Postfix(AbilityData ability, ref UnitCommand __result)
        {
            var unit = ability.Caster.Unit;
            if (!Main.Multiplayer.IsActive || !unit.IsDirectlyControllable)
            {
                return;
            }

            OnTacticalCommand(unit, __result);
        }

        private static void OnTacticalCommand(UnitEntityData unit, UnitCommand unitCommand)
        {
            try
            {
                switch (unitCommand)
                {
                    case TacticalCombatUnitUseAbility useAbility:
                        // TotalDefense action is handled separately
                        if (!string.Equals(useAbility.Ability.Blueprint.name, "ArmyTotalDefense", StringComparison.OrdinalIgnoreCase))
                        {
                            OnUseAbilityCommand(unit, useAbility);
                        }
                        break;
                    case TacticalCombatUnitAttack unitAttack:
                        OnUnitAttackCommand(unit, unitAttack);
                        break;
                    case UnitMoveTo unitMoveTo:
                        OnMoveToCommand(unit, unitMoveTo);
                        break;
                    default:
                        Main.GetLogger<TacticalCombatCommandsPatches>().LogWarning("Unhandled unit command. UnitId={UnitId}, Type={Type}", unit, unitCommand?.GetType().Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                Main.GetLogger<TacticalCombatCommandsPatches>().LogError(ex, "Unable to handle tactical command. UnitId={UnitId}, Type={Type}", unit?.UniqueId, unitCommand?.GetType().Name);
                throw;
            }
        }

        private static void OnUseAbilityCommand(UnitEntityData unit, TacticalCombatUnitUseAbility command)
        {
            var unitUseAbilityCommand = new NetworkTacticalUnitUseAbilityCommand
            {
                Ability = Main.Mapper.Map<NetworkAbility>(command.Ability),
                InitiatorUnitId = unit.UniqueId,
                Target = Main.Mapper.Map<NetworkTargetWrapper>(command.Target),
                VectorPath = GetPath(command.ForcedPath)
            };

            Main.Multiplayer.OnTacticalCombatUnitUseAbilityCommand(unitUseAbilityCommand);
        }

        private static void OnUnitAttackCommand(UnitEntityData unit, TacticalCombatUnitAttack command)
        {
            var unitAttackCommand = new NetworkTacticalUnitAttackCommand
            {
                UnitId = unit.UniqueId,
                Path = GetPath(command.ForcedPath),
                TargetUnitId = command.Target.UniqueId
            };

            Main.Multiplayer.OnTacticalCombatUnitAttackCommand(unitAttackCommand);
        }

        private static void OnMoveToCommand(UnitEntityData unit, UnitMoveTo command)
        {
            var moveToCommand = new NetworkTacticalUnitMoveToCommand
            {
                UnitId = unit.UniqueId,
                Path = GetPath(command.ForcedPath)
            };

            Main.Multiplayer.OnTacticalCombatUnitMoveToCommand(moveToCommand);
        }

        private static List<NetworkVector3> GetPath(Path commandPath)
        {
            var path = commandPath?.vectorPath.Select(v => v.ToNetworkVector3()).ToList() ?? [];
            return path;
        }
    }
}
