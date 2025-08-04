using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.View.MapObjects;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class AreaTransitionPatches
    {
        [HarmonyPatch(typeof(AreaTransitionPart), nameof(AreaTransitionPart.CheckRestrictions))]
        [HarmonyPostfix]
        public static void AreaTransitionPart_CheckRestrictions_Postfix(AreaTransitionPart __instance, UnitEntityData user, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || !__result)
            {
                return;
            }

            if (!Main.Multiplayer.CanLeaveArea())
            {
                Game.Instance.UI.Bark(user, UIStringConsts.GameNotifications.TryingToLeaveAsAClient, 10f);
                __result = false;
            }
        }
    }
}
