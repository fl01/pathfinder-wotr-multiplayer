using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Kingmaker.TurnBasedMode.Controllers;
using Kingmaker.View;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.HarmonyPatches.Clicks
{
    [HarmonyPatch]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public class ClicksPatches
    {
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

            var destination = new NetworkVector3(settings.Destination.x, settings.Destination.y, settings.Destination.z);
            Main.Multiplayer.MoveNonCombatCharacter(unit.UniqueId, destination, settings.Delay, settings.Orientation);
        }

        /// <summary>
        /// handles movement in combat
        /// </summary>
        [HarmonyPatch(typeof(ClickGroundHandler), nameof(ClickGroundHandler.OnClick), [typeof(GameObject), typeof(Vector3), typeof(int), typeof(bool), typeof(bool), typeof(bool)])]
        [HarmonyPostfix]
        public static void ClickGroundHandler_OnClick_Postfix(ClickUnitHandler __instance, bool __result, GameObject gameObject, Vector3 worldPosition, int button, bool simulate, bool muteEvents, bool IsTMBClick)
        {
            if (!Main.Multiplayer.IsActive || simulate || !__result)
            {
                return;
            }

            try
            {
                var click = CreateClick(gameObject, button, worldPosition, muteEvents, null);
                Main.Multiplayer.OnClickGround(click);
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<ClicksPatches>().LogError(ex, "Unable to process ClickGround click");
                throw;
            }
        }

        [HarmonyPatch(typeof(ClickWithSelectedAbilityHandler), nameof(ClickWithSelectedAbilityHandler.OnClick))]
        [HarmonyPrefix]
        public static void ClickWithSelectedAbilityHandler_OnClick_Prefix(ClickWithSelectedAbilityHandler __instance, bool simulate, out ClickAbilityState __state)
        {
            if (!Main.Multiplayer.IsActive || simulate)
            {
                __state = null;
                return;
            }

            // already set to null in postfix, but is required for network communication
            try
            {
                __state = new ClickAbilityState
                {
                    AbilityId = __instance.SelectedAbility?.UniqueId,
                    SpellbookId = __instance.SelectedAbility.Spellbook?.Blueprint.Name.Key
                };
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<ClicksPatches>().LogError(ex, "Unable to retrieve ability data");
                throw;
            }
        }

        [HarmonyPatch(typeof(ClickWithSelectedAbilityHandler), nameof(ClickWithSelectedAbilityHandler.OnClick))]
        [HarmonyPostfix]
        public static void ClickWithSelectedAbilityHandler_OnClick_Postfix(ClickWithSelectedAbilityHandler __instance, bool __result, ClickAbilityState __state, GameObject gameObject, Vector3 worldPosition, int button, bool simulate, bool muteEvents, bool IsTMBClick)
        {
            if (!Main.Multiplayer.IsActive || simulate || !__result)
            {
                return;
            }

            try
            {
                var click = CreateClick(gameObject, button, worldPosition, muteEvents, __state);
                Main.Multiplayer.OnClickWithSelectedAbility(click);
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<ClicksPatches>().LogError(ex, "Unable to process ClickAbility click");
                throw;
            }
        }

        [HarmonyPatch(typeof(ClickUnitHandler), nameof(ClickUnitHandler.OnClick))]
        [HarmonyPostfix]
        public static void ClickUnitHandler_OnClick_Postfix(ClickUnitHandler __instance, bool __result, GameObject gameObject, Vector3 worldPosition, int button, bool simulate, bool muteEvents, bool IsTMBClick)
        {
            if (!Main.Multiplayer.IsActive || simulate || !__result)
            {
                return;
            }

            try
            {
                var click = CreateClick(gameObject, button, worldPosition, muteEvents, null);
                Main.Multiplayer.OnClickUnit(click);
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<ClicksPatches>().LogError(ex, "Unable to process ClickUnit click");
                throw;
            }
        }

        private static NetworkClick CreateClick(GameObject gameObject, int button, UnityEngine.Vector3 worldPosition, bool muteEvents, ClickAbilityState state)
        {
            var selectedUnits = Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId)?.ToList();
            var targetUnitId = gameObject?.GetComponent<UnitEntityView>()?.UniqueId;
            var selectedUnit = Game.Instance.SelectionCharacter?.FirstSelectedUnit?.View;
            var path = selectedUnit == null ? null : PathVisualizer.Instance.CurrentPathForUnit(selectedUnit);

            return new NetworkClick
            {
                SelectedUnits = selectedUnits,
                TargetUnitId = targetUnitId,
                Button = button,
                WorldPosition = new NetworkVector3(worldPosition.x, worldPosition.y, worldPosition.z),
                MuteEvents = muteEvents,
                VectorPath = [.. path?.vectorPath?.Select(v => new NetworkVector3 { X = v.x, Y = v.y, Z = v.z }) ?? []],
                Ability = state == null ? null : new NetworkAbility
                {
                    Id = state.AbilityId,
                    SpellbookId = state.SpellbookId
                },
                ActionsState = CreateNetworkActionsState()
            };
        }

        private static NetworkActionsState CreateNetworkActionsState()
        {
            var actionStates = Game.Instance.TurnBasedCombatController.CurrentTurn?.GetActionsStates(Game.Instance.TurnBasedCombatController.CurrentTurn?.SelectedUnit);
            if (actionStates == null)
            {
                return null;
            }

            return new NetworkActionsState
            {
                ApproachPoint = new NetworkVector3(actionStates.ApproachPoint.x, actionStates.ApproachPoint.y, actionStates.ApproachPoint.z),
                ApproachRadius = actionStates.ApproachRadius,
                FiveFootStep = CreateNetworkCombatAction(actionStates.FiveFootStep),
                Free = CreateNetworkCombatAction(actionStates.Free),
                Standard = CreateNetworkCombatAction(actionStates.Standard),
                Swift = CreateNetworkCombatAction(actionStates.Swift),
                Move = CreateNetworkCombatAction(actionStates.Move),
            };
        }

        private static NetworkCombatAction CreateNetworkCombatAction(CombatAction action)
        {
            if (action == null)
            {
                return null;
            }

            return new NetworkCombatAction
            {
                MovementActivityStatePredicted = action.m_MovementActivityStatePredicted?.ToString(),
                MovementActivityStateCurrent = action.m_MovementActivityStateCurrent?.ToString(),
                AttackActivityStatePredicted = action.m_AttackActivityStatePredicted?.ToString(),
                AttackActivityStateCurrent = action.m_AttackActivityStateCurrent?.ToString(),
                AbilityActivityStatePredicted = action.m_AbilityActivityStatePredicted?.ToString(),
                AbilityActivityStateCurrent = action.m_AbilityActivityStateCurrent?.ToString(),
                LockType = action.LockType,
                HasMovePossibility = action.HasMovePossibility,
                MaxMoveDistance = action.MaxMoveDistance,
                RemainingMoveDistance = action.RemainingMoveDistance,
                PredictedMoveDistance = action.PredictedMoveDistance,
                Type = action.Type.ToString(),
            };
        }

        public class ClickAbilityState
        {
            public string AbilityId { get; set; }

            public string SpellbookId { get; set; }
        }
    }
}
