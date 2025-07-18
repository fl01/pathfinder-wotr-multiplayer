using HarmonyLib;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.View;
using UnityEngine;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.HarmonyPatches.Clicks
{
    [HarmonyPatch]
    public class ClicksPatches
    {
        [HarmonyPatch(typeof(ClickGroundHandler), nameof(ClickGroundHandler.RunCommand))]
        [HarmonyPrefix]
        public static void ClickGroundHandler_RunCommand_Prefix(UnitEntityData unit, ClickGroundHandler.CommandSettings settings)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.MoveCharacter(unit, settings);
        }

        [HarmonyPatch(typeof(ClickUnitHandler), nameof(ClickUnitHandler.OnClick))]
        [HarmonyPostfix]
        public static void ClickGroundHandler_OnClick_Postfix(ClickUnitHandler __instance, bool __result, GameObject gameObject, Vector3 worldPosition, int button, bool simulate, bool muteEvents, bool IsTMBClick)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (simulate || !__result)
            {
                return;
            }

            var targetUnitId = gameObject.GetComponent<UnitEntityView>()?.UniqueId;
            var click = new NetworkClick
            {
                TargetUnitId = targetUnitId,
                Button = button,
                WorldPosition = new System.Numerics.Vector3(worldPosition.x, worldPosition.y, worldPosition.z),
                MuteEvents = muteEvents
            };

            Main.Multiplayer.OnClickUnit(click);
        }
    }
}
