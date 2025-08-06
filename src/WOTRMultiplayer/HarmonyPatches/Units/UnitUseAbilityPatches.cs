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
        [HarmonyPatch(typeof(UnitUseAbility), nameof(UnitUseAbility.OnStart))]
        [HarmonyPostfix]
        public static void UnitUseAbility_OnStart_Postfix(UnitUseAbility __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            OnAbilityUse(__instance);
        }

        private static void OnAbilityUse(UnitUseAbility command)
        {

            if (command.Ability.StickyTouch != null)
            {
                Main.GetLogger<UnitUseAbilityPatches>().LogWarning("Skipping ability use as it's a part of another usage. UnitId={unitId}, AbilityName={abilityName}, AbilityId={abilityId}", command.Executor.UniqueId, command.Ability.Name, command.Ability.UniqueId);
                return;
            }

            var path = PathVisualizer.Instance.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => new NetworkVector3(v.x, v.y, v.z)).ToList();
            var networkAbility = new NetworkAbility
            {
                Id = command.Ability.UniqueId,
                Name = command.Ability.NameForAcronym,
                SpellbookId = command.Ability.Spellbook?.Blueprint.Name.Key,
                CasterId = command.Executor.UniqueId,
                TargetId = command.Target?.Unit?.UniqueId,
                TargetPoint = command.Target?.Point == null ? null : new NetworkVector3(command.Target.Point.x, command.Target.Point.y, command.Target.Point.z),
                VectorPath = networkPath,
                ActionsState = Main.Multiplayer.GetActionsState(),
                CommandType = command.Type.ToString(),
                ConvertedFromId = command.Ability.ConvertedFrom?.UniqueId,
            };

            Main.Multiplayer.OnAbilityUse(networkAbility);
        }
    }
}
