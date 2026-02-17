using System;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.UnitLogic.Abilities.Components.TargetCheckers;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Combat;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class ActivatableAbilityPatches
    {
        [HarmonyPatch(typeof(ActivatableAbility), nameof(ActivatableAbility.SetIsOn))]
        [HarmonyPostfix]
        public static void ActivatableAbility_SetIsOn_Postfix(ActivatableAbility __instance, UnitEntityData target)
        {
            if (!Main.Multiplayer.IsActive
                || Game.Instance.CurrentMode == GameModeType.None
                || PatchesUtils.IsHelperUnit(__instance.Owner.Unit.UniqueId))
            {
                return;
            }

            try
            {
                if (IsMountAbility(__instance) && ShouldSkipToggleMountActivationStage(__instance.IsOn, __instance.Owner.Unit))
                {
                    Main.GetLogger<ActivatableAbilityPatches>().LogInformation("Mount toggle is ignored. IsActive={IsActive}, TargetId={TargetId}", __instance.m_IsOn, target?.UniqueId);
                    return;
                }

                var caster = __instance.Owner.Unit;
                var ability = new NetworkActivatableAbility
                {
                    Id = __instance.UniqueId,
                    BlueprintId = __instance.Blueprint.AssetGuid.ToString(),
                    Name = __instance.NameForAcronym,
                    CasterId = caster.UniqueId,
                    TargetId = target?.UniqueId,
                    IsActive = __instance.m_IsOn
                };

                var shiftersFuryPart = __instance.Owner.Unit.Get<ShiftersFuryPart>();
                if (shiftersFuryPart != null)
                {
                    ability.ShifterFuryIndex = shiftersFuryPart.AppliedFacts.IndexOf(__instance);
                }

                Main.Multiplayer.OnToggleActivatableAbility(ability);
            }
            catch (Exception ex)
            {
                Main.GetLogger<ActivatableAbilityPatches>().LogError(ex, "Unable to handle activatable ability");
                throw;
            }
        }

        private static bool ShouldSkipToggleMountActivationStage(bool isOn, UnitEntityData unit)
        {
            // there is a separate 'UsaAbility' message sent when you actually use your 'Saddle Up' ability on a mount.
            // we need to filter everything so as not to send duplicate messages = we are only interested in a case when you dismount by toggling off 'Saddle Up' ability

            // it's ON when:
            // 1. you clicked on the 'Saddle Up' ability, but didn't select a target - nothing to communicate yet
            // 2. you are already mounted after using the 'Saddle up' ability on a mount - this is a part of 'Mounting' process handled by the game.
            //    Other players received the 'UseAbility' message, so their game goes through the same process right now
            // both cases should be ignored as they do not need to be communicated to other players
            if (isOn)
            {
                return true;
            }

            // it's OFF when:
            // 1. 'Saddle up' has been just used on a mount, so your ability becomes toggled off (it will become ON once you are mounted, see 'isOn' case 2)
            // 2. toggled off 'Saddle up' - there are two stages when this handler is called, but we need to catch only the "initiation" part
            //   2.a - you are starting to dismount - your mount (SaddledUnit) is still assigned to you
            //   2.b - dismounting has been finished - you have no mount assigned to you
            if (unit.RiderPart?.SaddledUnit == null)
            {
                return true;
            }

            return false;
        }

        private static bool IsMountAbility(ActivatableAbility activatableAbility)
        {
            var isMount = activatableAbility.Blueprint.SelectTargetAbility?.TargetRestrictions.Any(r => r is AbilityTargetIsSuitableMount);
            return isMount ?? false;
        }
    }
}
