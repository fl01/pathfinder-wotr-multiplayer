using System;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Kingmaker.UI.MVVM._VM.Lockpick;
using Kingmaker.View;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.HarmonyPatches.Combat;

namespace WOTRMultiplayer.HarmonyPatches.Clicks
{
    [HarmonyPatch]
    public class ClicksPatches
    {
        [HarmonyPatch(typeof(ClickGroundHandler), nameof(ClickGroundHandler.MoveSelectedUnitsToPoint), [typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool), typeof(float), typeof(bool), typeof(Action<UnitEntityData, ClickGroundHandler.CommandSettings>)])]
        [HarmonyPrefix]
        public static bool ClickGroundHandler_MoveSelectedUnitsToPoint_Prefix(Vector3 worldPosition, Vector3 direction, bool preview, bool showTargetMarker, float formationSpaceFactor, bool ignoreHold, Action<UnitEntityData, ClickGroundHandler.CommandSettings> commandRunner)
        {
            if (!Main.Multiplayer.IsActive || Main.Multiplayer.RemoteContext?.SelectedUnits == null)
            {
                return true;
            }

            var unitsToMove = Main.Multiplayer.RemoteContext.SelectedUnits;
            ClickGroundHandlerEx.MoveSelectedUnitsToPoint(unitsToMove, worldPosition, direction, preview, showTargetMarker, formationSpaceFactor, ignoreHold, commandRunner);
            return false;
        }

        [HarmonyPatch(typeof(ClickGroundHandler), nameof(ClickGroundHandler.RunCommand))]
        [HarmonyPrefix]
        public static void ClickGroundHandler_RunCommand_Prefix(UnitEntityData unit, ClickGroundHandler.CommandSettings settings)
        {
            if (!Main.Multiplayer.IsActive || TurnControllerPatches.IsSimulation.Value)
            {
                return;
            }

            var movementLimit = Game.Instance.TurnBasedCombatController.CurrentTurn?.CurrentMovementLimit;
            var path = PathVisualizer.Instance?.m_CurrentPath?.vectorPath;
            var attackMode = Game.Instance.TurnBasedCombatController.CurrentTurn?.m_AttackMode;
            var unitMoveTo = new NetworkUnitMoveTo
            {
                InitiatorUnitId = unit.UniqueId,
                Destination = settings.Destination.ToNetworkVector3(),
                MovementDelay = settings.Delay,
                Orientation = settings.Orientation,
                MovementLimit = movementLimit?.ToString(),
                VectorPath = [.. path?.Select(x => x.ToNetworkVector3()) ?? []],
                AttackMode = attackMode?.ToString(),
                SpeedLimit = settings.SpeedLimit,
                ApplySpeedLimitInCombat = settings.ApplySpeedLimitInCombat
            };
            Main.Multiplayer.OnUnitMoveTo(unitMoveTo);
        }

        [HarmonyPatch(typeof(ClickMapObjectHandler), nameof(ClickMapObjectHandler.OnClick), [typeof(GameObject), typeof(Vector3), typeof(int), typeof(bool), typeof(bool), typeof(bool)])]
        [HarmonyPostfix]
        public static void ClickMapObjectHandler_OnClick_Postfix(bool __result, GameObject gameObject, Vector3 worldPosition, int button, bool simulate, bool muteEvents, bool IsTMBClick)
        {
            if (!Main.Multiplayer.IsActive || simulate || !__result || gameObject != null && LockpickVM.NeedLockpick(gameObject.GetComponent<MapObjectView>()))
            {
                return;
            }

            try
            {
                var click = CreateClick(gameObject, button, worldPosition, muteEvents, IsTMBClick);
                Main.Multiplayer.OnClickMapObject(click);
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<ClicksPatches>().LogError(ex, "Unable to process ClickGround click");
                throw;
            }
        }

        private static NetworkClick CreateClick(GameObject gameObject, int button, Vector3 worldPosition, bool muteEvents, bool isTMBClick)
        {
            var selectedUnits = Game.Instance.SelectionCharacter.SelectedUnits?.Select(x => x.UniqueId).ToList() ?? [];
            var targetUnitId = gameObject?.GetComponent<UnitEntityView>()?.UniqueId;
            var mapObject = gameObject?.GetComponent<MapObjectView>();
            var selectedUnit = Game.Instance.SelectionCharacter?.FirstSelectedUnit?.View;
            var path = selectedUnit == null ? null : PathVisualizer.Instance?.CurrentPathForUnit(selectedUnit);

            return new NetworkClick
            {
                SelectedUnits = selectedUnits,
                TargetUnitId = targetUnitId,
                MapObjectId = mapObject?.UniqueId,
                IsLootBagMapObject = mapObject?.Data is DroppedLoot.EntityData,
                IsTMBClick = isTMBClick,
                Button = button,
                WorldPosition = worldPosition.ToNetworkVector3(),
                MuteEvents = muteEvents,
                VectorPath = [.. path?.vectorPath?.Select(v => v.ToNetworkVector3()) ?? []],
                MovementLimit = Game.Instance.TurnBasedCombatController.CurrentTurn?.CurrentMovementLimit.ToString()
            };
        }
    }
}
