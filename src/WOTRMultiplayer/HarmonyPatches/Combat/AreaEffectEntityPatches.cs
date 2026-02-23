using HarmonyLib;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using WOTRMultiplayer.Entities.AreaEffects;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class AreaEffectEntityPatches
    {
        [HarmonyPatch(typeof(AbilityAreaEffectLogic), nameof(AbilityAreaEffectLogic.HandleRound))]
        [HarmonyPrefix]
        public static bool AreaEffectEntityData_HandleRound_Prefix(AreaEffectEntityData areaEffect)
        {
            if (!Main.Multiplayer.IsActive || TacticalCombatHelper.IsActive)
            {
                return true;
            }

            var networkAreaEffect = Main.Mapper.Map<NetworkAreaEffect>(areaEffect);
            var canContinue = Main.Multiplayer.OnAreaEffectTriggered(networkAreaEffect);
            return canContinue;
        }
    }
}
