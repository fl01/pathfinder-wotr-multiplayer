using HarmonyLib;
using JetBrains.Annotations;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Abilities
{
    [HarmonyPatch]
    public class ActivatableAbilityPatches
    {
        [HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.SetIsOn))]
        [HarmonyPostfix]
        public static void ActivatableAbility_SetIsOn_Postfix(ActivatableAbility __instance, bool value, [CanBeNull] UnitEntityData target)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (__instance.m_IsOn)
            {
                Main.GetLogger<ActivatableAbilityPatches>().LogInformation("Started activatable ability. Name={name}, Id={abilityId}, UnitId={unitId}, UnitName={unitName}", __instance.NameForAcronym, __instance.UniqueId, __instance.Owner.Unit.UniqueId, __instance.Owner.Unit.CharacterName);
            }
            else
            {
                Main.GetLogger<ActivatableAbilityPatches>().LogInformation("Stopped activatable ability. Name={name}, Id={abilityId}, UnitId={unitId}, UnitName={unitName}", __instance.NameForAcronym, __instance.UniqueId, __instance.Owner.Unit.UniqueId, __instance.Owner.Unit.CharacterName);
            }
        }
    }
}
