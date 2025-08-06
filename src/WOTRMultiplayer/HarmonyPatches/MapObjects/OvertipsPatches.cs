using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Code.UI._ConsoleUI.Overtips;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.UI.Common;
using WOTRMultiplayer.MP.Entities.MapObjects;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    /// don't be confused by Kingmaker.UI._ConsoleUI.Overtips namespace, it's still being used on PC
    [HarmonyPatch]
    public class OvertipsPatches
    {
        [HarmonyPatch(typeof(ObjectInteractionOvertipView), nameof(ObjectInteractionOvertipView.OnClick))]
        [HarmonyPrefix]
        public static void ObjectInteractionOvertipView_OnClick_Prefix(ObjectInteractionOvertipView __instance)
        {
            var selectedUnits = Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId).ToList();
            var networkOvertip = new NetworkOvertip
            {
                MapObject = new NetworkMapObject
                {
                    Id = __instance.MapObjectView.UniqueId,
                    Position = new MP.Entities.NetworkVector3(__instance.MapObjectView.Data.Position.x, __instance.MapObjectView.Data.Position.y, __instance.MapObjectView.Data.Position.z)
                },
                Units = [.. Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId)]
            };

            Main.Multiplayer.OnInteractWithMapObjectOvertip(networkOvertip);
        }

        [HarmonyPatch(typeof(OvertipViewPartName), nameof(OvertipViewPartName.SetName))]
        [HarmonyPostfix]
        public static void OvertipViewPartName_SetName_Prefix(OvertipViewPartName __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var viewModel = __instance.GetViewModel() as EntityOvertipVM;
            if (viewModel == null || viewModel.Unit == null)
            {
                return;
            }

            var text = string.IsNullOrEmpty(viewModel.CustomName.Value) ? viewModel.Name.Value : viewModel.CustomName.Value;

            var mpOwnerName = Main.Multiplayer.GetMultiplayerOwnerName(viewModel.Unit.UniqueId);
            if (!string.IsNullOrEmpty(mpOwnerName))
            {
                text += $" ({mpOwnerName})";
            }

            if (Main.AddUnitIdToOvertip)
            {
                text += $" [{viewModel.Unit.UniqueId}]";
            }

            var formattedText = UIUtility.GetSaberBookFormat(text, __instance.m_SaberColor, 140, null, 0f);
            __instance.m_CharacterName.text = formattedText;
        }
    }
}
