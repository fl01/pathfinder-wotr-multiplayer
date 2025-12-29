using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Code.UI._ConsoleUI.Overtips;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.UI.Common;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.MapObjects;

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
            var networkOvertip = new NetworkOvertip
            {
                MapObject = CreateMapObject(__instance),
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

            if (__instance.GetViewModel() is not EntityOvertipVM viewModel || viewModel.Unit == null)
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

        [HarmonyPatch(typeof(EntityOvertipVM), nameof(EntityOvertipVM.StartAreaTransition))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> EntityOvertipVM_StartAreaTransition_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(CommonTranspilerReplacements), nameof(CommonTranspilerReplacements.GetPartyCharactersForGroupCommand));
            var lookFor = AccessTools.Method(typeof(Player), nameof(Player.GetPartyCharactersForGroupCommand));
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
                new(OpCodes.Ldloc_2),
                new(OpCodes.Ldc_I4_1),
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

            var networkOvertip = new NetworkOvertip
            {
                MapObject = CreateMapObject(__instance),
                Units = [.. CommonTranspilerReplacements.GetPartyCharactersForGroupCommand(__instance.MapObjectView.Data.Position, true).Select(x => x.UniqueId)],
                RequiresEveryoneToMoveMove = true
            };

            Main.Multiplayer.OnInteractWithMapObjectOvertip(networkOvertip);
        }

        [HarmonyPatch(typeof(AreaTransitionOvertipView), nameof(AreaTransitionOvertipView.OnClick))]
        [HarmonyPostfix]
        public static void AreaTransitionOvertipView_OnClick_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.ResetExecutionContext();
        }

        private static NetworkMapObject CreateMapObject(ObjectOvertipViewBase view)
        {
            var mapObject = new NetworkMapObject
            {
                Id = view.MapObjectView.UniqueId,
                Position = new NetworkVector3(view.MapObjectView.Data.Position.x, view.MapObjectView.Data.Position.y, view.MapObjectView.Data.Position.z)
            };

            return mapObject;
        }
    }
}
