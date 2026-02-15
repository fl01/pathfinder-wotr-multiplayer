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
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Extensions;

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

        /// <summary>
        /// handles movement outside of combat since it runs after formation calculations
        /// could be merged with ClickGroundHandler_OnClick_Postfix, but it requires repeating formation calculations
        /// </summary>
        [HarmonyPatch(typeof(ClickGroundHandler), nameof(ClickGroundHandler.RunCommand))]
        [HarmonyPrefix]
        public static void ClickGroundHandler_RunCommand_Prefix(UnitEntityData unit, ClickGroundHandler.CommandSettings settings)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var move = new NetworkCharacterMove
            {
                UnitId = unit.UniqueId,
                Delay = settings.Delay,
                Destination = settings.Destination.ToNetworkVector3(),
                Orientation = settings.Orientation
            };
            Main.Multiplayer.MoveNonCombatCharacter(move);
        }

        /// <summary>
        /// handles movement in combat
        /// </summary>
        [HarmonyPatch(typeof(ClickGroundHandler), nameof(ClickGroundHandler.OnClick), [typeof(GameObject), typeof(Vector3), typeof(int), typeof(bool), typeof(bool), typeof(bool)])]
        [HarmonyPostfix]
        public static void ClickGroundHandler_OnClick_Postfix(bool __result, GameObject gameObject, Vector3 worldPosition, int button, bool simulate, bool muteEvents, bool IsTMBClick)
        {
            if (!Main.Multiplayer.IsActive || simulate || !__result)
            {
                return;
            }

            try
            {
                var click = CreateClick(gameObject, button, worldPosition, muteEvents, IsTMBClick);
                Main.Multiplayer.OnClickGround(click);
            }
            catch (Exception ex)
            {
                Main.GetLogger<ClicksPatches>().LogError(ex, "Unable to process ClickGround click");
                throw;
            }
        }

        [HarmonyPatch(typeof(ClickUnitHandler), nameof(ClickUnitHandler.OnClick))]
        [HarmonyPostfix]
        public static void ClickUnitHandler_OnClick_Postfix(bool __result, GameObject gameObject, Vector3 worldPosition, int button, bool simulate, bool muteEvents, bool IsTMBClick)
        {
            if (!Main.Multiplayer.IsActive || simulate || !__result)
            {
                return;
            }

            try
            {
                var click = CreateClick(gameObject, button, worldPosition, muteEvents, IsTMBClick);
                Main.Multiplayer.OnClickUnit(click);
            }
            catch (Exception ex)
            {
                Main.GetLogger<ClicksPatches>().LogError(ex, "Unable to process ClickUnit click");
                throw;
            }
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
