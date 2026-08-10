using System;
using HarmonyLib;
using Kingmaker.Controllers.Units;
using Kingmaker.UnitLogic.Parts;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class SaddledUnitControllerPatches
    {
        [HarmonyPatch(typeof(SaddledUnitController), nameof(SaddledUnitController.TickDelegateRiderToMount))]
        [HarmonyPostfix]
        public static void SaddledUnitController_TickDelegateRiderToMount_Postfix(UnitPartRider riderPart)
        {
            if (!Main.Multiplayer.IsActive
                || !Main.Multiplayer.IsControlledByPlayers(riderPart.Owner.UniqueId)
                || Main.Multiplayer.IsControlledByLocalPlayer(riderPart.Owner.UniqueId)
                || !Main.Multiplayer.IsInCombat)
            {
                return;
            }

            try
            {
                var attackCommand = riderPart.Owner.Commands.Attack;
                if (attackCommand != null && !attackCommand.IsStarted && attackCommand.CreatedByPlayer && riderPart.SaddledUnit.Commands.Attack != null)
                {
                    riderPart.SaddledUnit.Commands.Attack.ForceFullAttack = attackCommand.ForceFullAttack;
                    riderPart.SaddledUnit.Commands.Attack.IsSingleAttack = attackCommand.IsSingleAttack;
                }
            }
            catch (Exception ex)
            {
                Main.GetLogger<SaddledUnitControllerPatches>().LogError(ex, "Error after ticking rider->mount delegate");
                throw;
            }
        }
    }
}
