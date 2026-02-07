using HarmonyLib;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using WOTRMultiplayer.Entities.Combat;

namespace WOTRMultiplayer.HarmonyPatches.ActionBar
{
    [HarmonyPatch]
    public class MechanicActionBarSlotAbilityPatches
    {
        [HarmonyPatch(typeof(UnitBrain), nameof(UnitBrain.AutoUseAbility), MethodType.Setter)]
        [HarmonyPrefix]
        public static void UnitBrain_AutoUseAbilitySetter_Prefix(UnitBrain __instance, AbilityData value)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var networkAbility = value == null ? null : new NetworkAbility
            {
                Id = value.UniqueId,
                Name = value.NameForAcronym,
                SpellbookId = value.Spellbook?.Blueprint.Name.Key,
                CasterId = __instance.Owner.UniqueId,
                ConvertedFromId = value.ConvertedFrom?.UniqueId
            };

            Main.Multiplayer.OnUnitAutoUseAbilityChanged(__instance.Owner.UniqueId, networkAbility);
        }
    }
}
