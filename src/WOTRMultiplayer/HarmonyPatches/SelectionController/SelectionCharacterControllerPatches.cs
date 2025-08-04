using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Entities;

namespace WOTRMultiplayer.HarmonyPatches.SelectionController
{
    [HarmonyPatch]
    public class SelectionCharacterControllerPatches
    {
        [HarmonyPatch(typeof(SelectionCharacterController), nameof(SelectionCharacterController.SelectedUnits), MethodType.Getter)]
        [HarmonyPrefix]
        public static bool SelectionCharacterController_SelectedUnits_Prefix(SelectionCharacterController __instance, ref List<UnitEntityData> __result)
        {
            if (!Main.Multiplayer.IsActive || Main.Multiplayer.ExecutionContext?.SelectedUnits == null)
            {
                return true;
            }

            __result = Main.Multiplayer.ExecutionContext.SelectedUnits;
            return false;
        }
    }
}
