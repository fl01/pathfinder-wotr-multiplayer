using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Code.UI._ConsoleUI.Overtips;
using WOTRMultiplayer.MP.Entities.MapObjects;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class OvertipsPatches
    {
        /// <summary>
        /// don't be confused by Kingmaker.UI._ConsoleUI.Overtips namespace, it's still being used on PC
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="interactionPart"></param>
        [HarmonyPatch(typeof(ObjectInteractionOvertipView), nameof(ObjectInteractionOvertipView.OnClick))]
        [HarmonyPrefix]
        public static void ObjectInteractionOvertipView_OnClick_Prefix(ObjectInteractionOvertipView __instance)
        {
            var selectedUnits = Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId).ToList();
            var networkOvertip = new NetworkOvertip
            {
                MapObjectId = __instance.MapObjectView.UniqueId,
                Units = [.. Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId)]
            };

            Main.Multiplayer.OnInteractWithMapObjectOvertip(networkOvertip);
        }
    }
}
