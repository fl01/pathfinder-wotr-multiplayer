using HarmonyLib;
using Kingmaker.RuleSystem.Rules.Damage;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleHealDamagePatches
    {
        [HarmonyPatch(typeof(RuleHealDamage), nameof(RuleHealDamage.Roll))]
        [HarmonyPostfix]
        public static void RuleHealDamage_Roll_Postfix(RuleHealDamage __instance, int unitsCount, ref int __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __result = Main.Rolls.OnAfterRollRuleHealDamage(__instance, unitsCount, __result);
        }
    }
}
