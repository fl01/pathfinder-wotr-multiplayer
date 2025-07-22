using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Kingmaker.View;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.HarmonyPatches.Clicks
{
    [HarmonyPatch]
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

            var click = CreateClick(gameObject, button, worldPosition, muteEvents, null);

            Main.Multiplayer.OnClickGround(click);
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

            var click = CreateClick(gameObject, button, worldPosition, muteEvents, __state);

            Main.Multiplayer.OnClickWithSelectedAbility(click);
        }

        [HarmonyPatch(typeof(ClickUnitHandler), nameof(ClickUnitHandler.OnClick))]
        [HarmonyPostfix]
        public static void ClickUnitHandler_OnClick_Postfix(ClickUnitHandler __instance, bool __result, GameObject gameObject, Vector3 worldPosition, int button, bool simulate, bool muteEvents, bool IsTMBClick)
        {
            if (!Main.Multiplayer.IsActive || simulate || !__result)
            {
                return;
            }

            var click = CreateClick(gameObject, button, worldPosition, muteEvents, null);

            Main.Multiplayer.OnClickUnit(click);
        }

        private static NetworkClick CreateClick(GameObject gameObject, int button, UnityEngine.Vector3 worldPosition, bool muteEvents, ClickAbilityState state)
        {
            var selectedUnits = Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId)?.ToList();
            var targetUnitId = gameObject?.GetComponent<UnitEntityView>()?.UniqueId;
            var path = PathVisualizer.Instance.CurrentPathForUnit(Game.Instance.SelectionCharacter.FirstSelectedUnit?.View);
            var actionStates = Game.Instance.TurnBasedCombatController.CurrentTurn?.GetActionsStates(Game.Instance.TurnBasedCombatController.CurrentTurn?.SelectedUnit);
            Main.GetLogger<ClicksPatches>().LogInformation("Unit action states. UnitId={unitID}, ApproachPoint={approachPoint}, ApproachRadius={approachRadius}", Game.Instance.TurnBasedCombatController.CurrentTurn?.SelectedUnit?.UniqueId, actionStates?.ApproachPoint, actionStates?.ApproachRadius);

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
                ActionsState = new NetworkActionsState
                {
                    ApproachPoint = actionStates?.ApproachPoint == null ? null : new NetworkVector3(actionStates.ApproachPoint.x, actionStates.ApproachPoint.y, actionStates.ApproachPoint.z),
                    ApproachRadius = actionStates?.ApproachRadius ?? 0
                }
            };
        }

        public class ClickAbilityState
        {
            public string AbilityId { get; set; }

            public string SpellbookId { get; set; }
        }
    }
}
