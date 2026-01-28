using HarmonyLib;
using Kingmaker.Controllers.Combat;
using Kingmaker.EntitySystem.Entities;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitCombatStatePatches
    {
        [HarmonyPatch(typeof(UnitCombatState), nameof(UnitCombatState.AttackOfOpportunity))]
        [HarmonyPostfix]
        public static void UnitCombatState_AttackOfOpportunity_Postfix(UnitCombatState __instance, UnitEntityData target, bool tricksterAttack, bool simulate, bool disengaging, bool afterDefensivelyFail, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || simulate)
            {
                return;
            }

            Main.GetLogger<UnitCombatStatePatches>().LogWarning("AoO: UnitId={UnitId}, TargetUnitId={TargetUnitId}, Result={Result}, TricksterAttack={TricksterAttack}, Disengaging={Disengaging}, AfterDefensivelyFail={AfterDefensivelyFail}, UnitPreventAoO={UnitPreventAoO}, TargetPreventAoO={TargetPreventAoO}",
                __instance.Unit.UniqueId, target?.UniqueId, __result, tricksterAttack, disengaging, afterDefensivelyFail, __instance.PreventAttacksOfOpporunityNextFrame, target.CombatState.PreventAttacksOfOpporunityNextFrame);
        }
    }
}
