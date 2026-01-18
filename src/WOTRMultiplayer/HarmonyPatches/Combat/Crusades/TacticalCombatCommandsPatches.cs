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
            switch (unitCommand)
            {
                case TacticalCombatUnitUseAbility useAbility:
                    OnUseAbilityCommand(useAbility);
                    break;
                case TacticalCombatUnitAttack unitAttack:
                    OnUnitAttackCommand(unit, unitAttack);
                    break;
                case UnitMoveTo unitMoveTo:
                    OnMoveToCommand(unit, unitMoveTo);
                    break;
                default:
                    Main.GetLogger<TacticalCombatCommandsPatches>().LogWarning("Unhandled unit command. Type={Type}", unitCommand?.GetType().Name);
                    break;
            }
        }

        private static void OnUseAbilityCommand(TacticalCombatUnitUseAbility command)
        {
            var unitUseAbilityCommand = new NetworkTacticalUnitUseAbilityCommand
            {
                Ability = GetAbility(command),
            };

            Main.Multiplayer.OnCrusadeTacticalUnitUseAbilityCommand(unitUseAbilityCommand);
        }

        private static NetworkAbility GetAbility(TacticalCombatUnitUseAbility command)
        {
            var ability = new NetworkAbility
            {
                Id = command.Ability.UniqueId,
                Name = command.Ability.NameForAcronym,
                SpellbookId = command.Ability.Spellbook?.Blueprint.Name.Key,
                CasterId = command.Executor.UniqueId,
                VectorPath = GetPath(command.ForcedPath),
                TargetId = command.Target?.Unit?.UniqueId,
                TargetPoint = command.Target?.Point == null ? null : new NetworkVector3(command.Target.Point.x, command.Target.Point.y, command.Target.Point.z),
                ConvertedFromId = command.Ability.ConvertedFrom?.UniqueId
            };

            return ability;
        }

        private static void OnUnitAttackCommand(UnitEntityData unit, TacticalCombatUnitAttack command)
        {
            var unitAttackCommand = new NetworkTacticalUnitAttackCommand
            {
                UnitId = unit.UniqueId,
                Path = GetPath(command.ForcedPath),
                TargetUnitId = command.Target.UniqueId
            };

            Main.Multiplayer.OnCrusadeTacticalUnitAttackCommand(unitAttackCommand);
        }

        private static void OnMoveToCommand(UnitEntityData unit, UnitMoveTo command)
        {
            var moveToCommand = new NetworkTacticalUnitMoveToCommand
            {
                UnitId = unit.UniqueId,
                Path = GetPath(command.ForcedPath)
            };

            Main.Multiplayer.OnCrusadeTacticalUnitMoveToCommand(moveToCommand);
        }

        private static List<NetworkVector3> GetPath(Path commandPath)
        {
            var path = commandPath?.vectorPath.Select(v => new NetworkVector3(v.x, v.y, v.z)).ToList() ?? [];
            return path;
        }
    }
}
