using System.Linq;
using HarmonyLib;
using Kingmaker.TurnBasedMode;
using Kingmaker.UnitLogic.Commands;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;

namespace WOTRMultiplayer.HarmonyPatches.Units
{
    [HarmonyPatch]
    public class UnitUseAbilityPatches
    {
        [HarmonyPatch(typeof(UnitUseAbility), nameof(UnitUseAbility.TriggerAnimation))]
        [HarmonyPrefix]
        public static void UnitUseAbility_TriggerAnimation_Prefix(UnitUseAbility __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (__instance.Ability.StickyTouch != null)
            {
                Main.GetLogger<UnitUseAbilityPatches>().LogWarning("Skipping ability use as it's a part of another usage. UnitId={unitId}, AbilityName={abilityName}, AbilityId={abilityId}", __instance.Executor.UniqueId, __instance.Ability.Name, __instance.Ability.UniqueId);
                return;
            }

            var path = PathVisualizer.Instance.CurrentPathForUnit(__instance.Executor.View);
            var networkPath = path?.vectorPath.Select(v => new NetworkVector3(v.x, v.y, v.z)).ToList();
            var networkAbility = new NetworkAbility
            {
                Id = __instance.Ability.UniqueId,
                Name = __instance.Ability.NameForAcronym,
                SpellbookId = __instance.Ability.Spellbook?.Blueprint.Name.Key,
                CasterId = __instance.Executor.UniqueId,
                TargetId = __instance.Target?.Unit?.UniqueId,
                TargetPoint = __instance.Target?.Point == null ? null : new NetworkVector3(__instance.Target.Point.x, __instance.Target.Point.y, __instance.Target.Point.z),
                VectorPath = networkPath,
                ActionsState = Main.Multiplayer.GetActionsState(),
                CommandType = __instance.Type.ToString(),
                ConvertedFromId = __instance.Ability.ConvertedFrom?.UniqueId,
            };

            Main.Multiplayer.OnAbilityUse(networkAbility);
        }
    }
}
