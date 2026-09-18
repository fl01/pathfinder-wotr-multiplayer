using HarmonyLib;
using Kingmaker.Armies.TacticalCombat.Controllers;
using Kingmaker.EntitySystem.Entities;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class TacticalCombatUnitLifeControllerPatches
    {
        [HarmonyPatch(typeof(TacticalCombatUnitLifeController), nameof(TacticalCombatUnitLifeController.KillUnit))]
        [HarmonyPrefix]
        public static void TacticalCombatUnitLifeController_KillUnit_Prefix(UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnUnitDeath(unit.UniqueId, unit.GroupId);
        }
    }
}
