using HarmonyLib;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleCalculateDamagePatches
    {
        [HarmonyPatch(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleCalculateDamage_OnTrigger_Postfix(RuleCalculateDamage __instance, RulebookEventContext context)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnAfterRuleCalculateDamageTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.OnTrigger))]
        [HarmonyPrefix]
        public static bool RuleCalculateDamage_OnTrigger_Prefix(RuleCalculateDamage __instance, RulebookEventContext context)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var shouldRunOriginal = Main.Multiplayer.OnBeforeRuleCalculateDamageTrigger(__instance);
            return shouldRunOriginal;
        }
    }
}
