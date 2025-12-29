using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.MVVM._VM.Lockpick;
using WOTRMultiplayer.Entities;
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
                MapObject = new NetworkMapObject
                {
                    Id = __instance.LockpickMapObject.UniqueId,
                    Position = new NetworkVector3(__instance.LockpickMapObject.Data.Position.x, __instance.LockpickMapObject.Data.Position.y, __instance.LockpickMapObject.Data.Position.z),
                },
                LockpickType = type,
                Units = [.. Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId)]
            };

            Main.Multiplayer.OnLockpickInteraction(lockpickInteraction);
        }
    }
}
