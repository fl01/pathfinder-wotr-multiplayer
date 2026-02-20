using HarmonyLib;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace WOTRMultiplayer.HarmonyPatches.ContextActions
{
    [HarmonyPatch]
    public class ContextActionHideInPlainSightPatches
    {
        [HarmonyPatch(typeof(ContextActionHideInPlainSight), nameof(ContextActionHideInPlainSight.RunAction))]
        [HarmonyPrefix]
        public static void ContextActionHideInPlainSight_RunAction_Prefix(ContextActionHideInPlainSight __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnSetUnitStealthEnabled(__instance.Target.Unit.UniqueId, isEnabled: true, isForced: true);
        }
    }
}
