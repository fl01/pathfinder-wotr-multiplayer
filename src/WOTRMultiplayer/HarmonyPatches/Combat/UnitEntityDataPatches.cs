using HarmonyLib;
using Kingmaker.EntitySystem.Entities;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitEntityDataPatches
    {
        [HarmonyPatch(typeof(UnitEntityData), nameof(UnitEntityData.JoinCombat))]
        [HarmonyPrefix]
        public static bool UnitEntityData_JoinCombat_Prefix(UnitEntityData __instance, bool notSurprised)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanUnitJoinCombat(__instance.UniqueId);
            return canContinue;
        }
    }
}
