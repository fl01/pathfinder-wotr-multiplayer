using HarmonyLib;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitLifeControllerPatches
    {
        [HarmonyPatch(typeof(UnitLifeController), nameof(UnitLifeController.OnUnitDeath))]
        [HarmonyPostfix]
        public static void UnitLifeController_OnUnitDeath_Postfix(UnitEntityData unit, bool alreadyDead)
        {
            if (!Main.Multiplayer.IsActive || !unit.State.IsFinallyDead)
            {
                return;
            }

            Main.Multiplayer.OnUnitDeath(unit.UniqueId);
        }
    }
}
