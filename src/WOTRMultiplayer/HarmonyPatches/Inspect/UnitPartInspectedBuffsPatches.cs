using HarmonyLib;
using Kingmaker.UnitLogic.Parts;

namespace WOTRMultiplayer.HarmonyPatches.Inspect
{
    [HarmonyPatch]
    public class UnitPartInspectedBuffsPatches
    {
        [HarmonyPatch(typeof(UnitPartInspectedBuffs), nameof(UnitPartInspectedBuffs.MakeCheck))]
        [HarmonyPrefix]
        public static bool UnitPartInspectedBuffs_MakeCheck_Prefix(ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            __result = true;
            return false;
        }
    }
}
