using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Code.UI._ConsoleUI.Overtips;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.UI.Common;
using Kingmaker.UI.Selection;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    /// despite Kingmaker.UI._ConsoleUI.Overtips namespace name, it's still being used on PC
    [HarmonyPatch]
    public class OvertipsPatches
    {
        [HarmonyPatch(typeof(OvertipViewPartName), nameof(OvertipViewPartName.SetName))]
        [HarmonyPostfix]
        public static void OvertipViewPartName_SetName_Postfix(OvertipViewPartName __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (__instance.GetViewModel() is not EntityOvertipVM viewModel || viewModel.Unit == null)
            {
                return;
            }

            var text = string.IsNullOrEmpty(viewModel.CustomName.Value) ? viewModel.Name.Value : viewModel.CustomName.Value;

            var mpOwnerName = Main.Multiplayer.GetCharacterOwnerName(viewModel.Unit.UniqueId);
            if (!string.IsNullOrEmpty(mpOwnerName))
            {
                text += $" ({mpOwnerName})";
            }

            if (Main.ModManagerSettings.AddUnitIdToOvertip)
            {
                text += $" [{viewModel.Unit.UniqueId}]";
            }

            var formattedText = UIUtility.GetSaberBookFormat(text, __instance.m_SaberColor, 140, null, 0f);
            __instance.m_CharacterName.text = formattedText;
        }

        [HarmonyPatch(typeof(ObjectInteractionOvertipView), nameof(ObjectInteractionOvertipView.OnClick))]
        [HarmonyPrefix]
        public static void ObjectInteractionOvertipView_OnClick_Prefix(ObjectInteractionOvertipView __instance)
        {
            var networkOvertip = new NetworkOvertip
            {
                MapObject = CreateMapObject(__instance),
                Units = [.. Game.Instance.SelectionCharacter.SelectedUnits.Select(x => x.UniqueId)]
            };

            if (Main.Multiplayer.IsInCombat)
            {
                var activeUnit = Game.Instance.TurnBasedCombatController.CurrentTurn?.SelectedUnit;
                if (activeUnit != null)
                {
                    var path = PathVisualizer.Instance?.CurrentPathForUnit(activeUnit.View);
                    networkOvertip.VectorPath = path?.vectorPath?.Select(x => x.ToNetworkVector3()).ToList() ?? [];
                }
            }

            Main.Multiplayer.OnInteractWithMapObjectOvertip(networkOvertip);
        }

        [HarmonyPatch(typeof(EntityOvertipVM), nameof(EntityOvertipVM.StartAreaTransition))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityOvertipVM_StartAreaTransition_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(OvertipsPatches), nameof(OvertipsPatches.SelectAllCharactersControlledByLocalPlayer));
            var lookFor = AccessTools.Method(typeof(SelectionManagerBase), nameof(SelectionManagerBase.SelectAll));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<OvertipsPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            match = match.Advance(-4).RemoveInstructions(5);
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);

            Main.GetLogger<OvertipsPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(AreaTransitionOvertipView), nameof(AreaTransitionOvertipView.OnClick))]
        [HarmonyPrefix]
        public static void AreaTransitionOvertipView_OnClick_Prefix(AreaTransitionOvertipView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var units = Game.Instance.Player.PartyAndPets.Where(u => Main.Multiplayer.IsControlledByLocalPlayer(u.UniqueId)).Select(c => c.View).ToList();
            var networkOvertip = new NetworkOvertip
            {
                MapObject = CreateMapObject(__instance),
                Units = [.. units.Select(x => x.Data.UniqueId)]
            };

            Main.Multiplayer.OnInteractWithMapObjectOvertip(networkOvertip);
        }

        private static NetworkMapObject CreateMapObject(ObjectOvertipViewBase view)
        {
            var mapObject = new NetworkMapObject
            {
                Id = view.MapObjectView.UniqueId,
                Position = view.MapObjectView.Data.Position.ToNetworkVector3()
            };

            return mapObject;
        }

        private static void SelectAllCharactersControlledByLocalPlayer()
        {
            if (!Main.Multiplayer.IsActive)
            {
                Game.Instance.UI.SelectionManager.SelectAll();
                return;
            }

            if (Main.Multiplayer.RemoteContext?.SelectedUnits != null)
            {
                return;
            }

            List<UnitEntityData> units = [.. Game.Instance.Player.PartyAndPets.Where(u => Main.Multiplayer.IsControlledByLocalPlayer(u.UniqueId))];
            var views = units.Select(c => c.View).ToList();
            (Game.Instance.UI.SelectionManager as SelectionManagerPC).MultiSelect(views, true);
        }
    }
}
