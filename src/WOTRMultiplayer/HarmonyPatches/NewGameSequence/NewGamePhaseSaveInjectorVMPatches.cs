using HarmonyLib;
using Kingmaker.UI.MVVM._VM.NewGame;

namespace WOTRMultiplayer.HarmonyPatches.NewGameSequence
{
    [HarmonyPatch]
    public class NewGamePhaseSaveInjectorVMPatches
    {
        [HarmonyPatch(typeof(NewGamePhaseSaveInjectorVM), nameof(NewGamePhaseSaveInjectorVM.UpdateNeedShow))]
        [HarmonyPrefix]
        public static bool NewGamePhaseSaveInjectorVM_UpdateNeedShow_Prefix(NewGamePhaseSaveInjectorVM __instance, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
