using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.MVVM._VM.Lockpick;
using WOTRMultiplayer.Entities.MapObjects;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class LockPickVMPatches
    {
        [HarmonyPatch(typeof(LockpickVM), nameof(LockpickVM.OnInteraction))]
        [HarmonyPrefix]
        public static void LockpickVM_OnInteraction_Prefix(LockpickVM __instance, LockpickType type)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var lockpickInteraction = new NetworkLockpickInteraction
            {
                MapObject = Main.Mapper.Map<NetworkMapObject>(__instance.LockpickMapObject.Data),
                LockpickType = type,
                Units = [.. Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId)]
            };

            Main.Multiplayer.OnLockpickInteraction(lockpickInteraction);
        }
    }
}
