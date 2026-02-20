using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Combat;
using Kingmaker.UnitLogic.Groups;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitCombatLeaveControllerPatches
    {
        [HarmonyPatch(typeof(UnitCombatLeaveController), nameof(UnitCombatLeaveController.ShouldLeaveCombat))]
        [HarmonyPostfix]
        public static void UnitCombatLeaveController_ShouldLeaveCombat_Postfix(UnitGroup group, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || group != Game.Instance.Player.Group || !__result)
            {
                return;
            }

            var canLeaveCombat = Main.Multiplayer.CanLeaveCombat();
            if (!canLeaveCombat)
            {
                __result = false;
            }
        }
    }
}
