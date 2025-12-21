using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._VM.CharGen;
using Owlcat.Runtime.UI.Controls.Other;
using UniRx;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class RespecWindowPatches
    {
        [HarmonyPatch(typeof(RespecWindowPCView), nameof(RespecWindowPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void RespecWindowPCView_BindViewImplementation_Postfix(RespecWindowPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.AddDisposable(__instance.m_MythicUpButton.OnSingleLeftClickAsObservable().Subscribe(_ => Main.Multiplayer.OnLevelingRespecMythicLevelUp()));
            __instance.AddDisposable(__instance.m_LevelupButton.OnSingleLeftClickAsObservable().Subscribe(_ => Main.Multiplayer.OnLevelingRespecLevelUp()));

            __instance.AddDisposable(__instance.m_CompleteButton.OnSingleLeftClickAsObservable().Subscribe(_ => Main.Multiplayer.OnLevelingRespecCompleted()));
        }

        [HarmonyPatch(typeof(RespecWindowVM), nameof(RespecWindowVM.UpdateProperties))]
        [HarmonyPostfix]
        public static void RespecWindowVM_UpdateProperties_Postfix(RespecWindowVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var unitId = __instance.CurrentUnit.Value.UniqueId;
            Main.Multiplayer.OnLevelingRespecWindowShown(unitId);
        }
    }
}
