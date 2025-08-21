using System.Linq;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities.Components.TargetCheckers;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.MP.Entities.Abilities;

namespace WOTRMultiplayer.HarmonyPatches.Abilities
{
    [HarmonyPatch]
    public class ActivatableAbilityPatches
    {
        [HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.SetIsOn))]
        [HarmonyPostfix]
        public static void ActivatableAbility_SetIsOn_Postfix(ActivatableAbility __instance, UnitEntityData target)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (__instance.Blueprint.SelectTargetAbility != null &&
                __instance.Blueprint.SelectTargetAbility.TargetRestrictions.Any(r => r is AbilityTargetIsSuitableMount))
            {
                // mount toggle is messing up with AbilityUse, so we need to handle Dismount only as everything else is handled by AbilityUse
                if (__instance.m_IsOn
                    || !__instance.Owner.Unit.Buffs.Enumerable.Any(a => string.Equals(a.Blueprint.NameForAcronym, "MountedBuff", System.StringComparison.OrdinalIgnoreCase)))
                {
                    Main.GetLogger<ActivatableAbilityPatches>().LogInformation("Mount toggle is ignored. IsActive={IsActive}", __instance.m_IsOn);
                    return;
                }
            }

            if (PatchesUtils.IsHelperUnit(__instance.Owner.Unit.UniqueId))
            {
                // happens when you hover over enemy creature in top list (Turn based combat)
                return;
            }

            var ability = new NetworkActivatableAbility
            {
                Id = __instance.UniqueId,
                CasterId = __instance.Owner.Unit.UniqueId,
                TargetId = target?.UniqueId,
                IsActive = __instance.m_IsOn
            };

            Main.Multiplayer.OnToggleActivatableAbility(ability);
        }
    }
}
