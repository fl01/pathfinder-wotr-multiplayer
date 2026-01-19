using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.GameModes;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.GlobalMap;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Message;
using Kingmaker.UI.MVVM._VM.GlobalMap;
using Kingmaker.UI.MVVM._VM.GlobalMap.Message;
using Kingmaker.UI.MVVM._VM.GlobalMap.Movement;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class GlobalMapMovementPatches
    {
        [HarmonyPatch(typeof(GlobalMapCommonCanvas), nameof(GlobalMapCommonCanvas.Update))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapCommonCanvas_Update_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(GlobalMapMovementPatches), nameof(GlobalMapMovementPatches.OnFatiguePopupShown));
            var lookFor = AccessTools.Method(typeof(UIUtility), nameof(UIUtility.ShowMessageBox));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            match = match.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapMovementPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstruction = new List<CodeInstruction>()
            {
                new(OpCodes.Call, extraCall),
            };
            match = match.Advance(1).Insert(newInstruction);
            Main.GetLogger<GlobalMapMovementPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(GlobalMapCommonCanvas), nameof(GlobalMapCommonCanvas.OnFatiqueClose))]
        [HarmonyPrefix]
        public static void GlobalMapCommonCanvas_OnFatiqueClose_Prefix(MessageModalBase.ButtonType btn)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            OnFatigueMessageActionChoosen(btn);
        }

        [HarmonyPatch(typeof(GlobalMapVM), nameof(GlobalMapVM.OnUpdateFatigueHandler))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapVM_OnUpdateFatigueHandler_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(GlobalMapMovementPatches), nameof(GlobalMapMovementPatches.OnFatiguePopupShown));
            var lookFor = AccessTools.Method(typeof(UIUtility), nameof(UIUtility.ShowMessageBox));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            match = match.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapMovementPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstruction = new List<CodeInstruction>()
            {
                new(OpCodes.Call, extraCall),
            };
            match = match.Advance(1).Insert(newInstruction);
            Main.GetLogger<GlobalMapMovementPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(GlobalMapVM), nameof(GlobalMapVM.OnFatigueClose))]
        [HarmonyPrefix]
        public static void GlobalMapVM_OnFatigueClose_Prefix(MessageModalBase.ButtonType btn)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            OnFatigueMessageActionChoosen(btn);
        }

        [HarmonyPatch(typeof(GlobalMapController), nameof(GlobalMapController.BeginCombat))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapController_BeginCombat_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(GlobalMapMovementPatches), nameof(GlobalMapMovementPatches.OnShowFleeMessageBox));
            var lookFor = AccessTools.Method(typeof(UIUtility), nameof(UIUtility.ShowMessageBox));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            match = match.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapMovementPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }
            var newExactInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-19).RemoveInstructions(20).Insert(newExactInstructions);
            Main.GetLogger<GlobalMapMovementPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(GlobalMapMovementVM), nameof(GlobalMapMovementVM.TryEnterLocation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapMovementVM_TryEnterLocation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraDirectionalCall = AccessTools.Method(typeof(GlobalMapMovementPatches), nameof(GlobalMapMovementPatches.OnGlobalMapDirectionalMovement));
            var extraExactCall = AccessTools.Method(typeof(GlobalMapMovementPatches), nameof(GlobalMapMovementPatches.OnGlobalMapExactMovement));
            var lookFor = AccessTools.Method(typeof(IGlobalMapTraveler), nameof(IGlobalMapTraveler.StartTravel));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapMovementPatches>().LogError("Invalid transpiler position (GlobalMapDirectionalMovement). Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newDirectionalInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 4),
                new(OpCodes.Call, extraDirectionalCall),
            };
            match = match.Insert(newDirectionalInstructions);

            match = match.Advance(newDirectionalInstructions.Count).SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapMovementPatches>().LogError("Invalid transpiler position (GlobalMapExactMovement). Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }
            var newExactInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 5),
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Call, extraExactCall),
            };
            match = match.Insert(newExactInstructions);
            Main.GetLogger<GlobalMapMovementPatches>().LogInformation("Transpiler has been applied (GlobalMapDirectionalMovement + GlobalMapExactMovement). Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(GlobalMapView), nameof(GlobalMapView.GoToLocationRevealed))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapView_GoToLocationRevealed_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraExactCall = AccessTools.Method(typeof(GlobalMapMovementPatches), nameof(GlobalMapMovementPatches.OnGlobalMapExactMovement));
            var lookFor = AccessTools.Method(typeof(GlobalMapPlayerState), nameof(GlobalMapPlayerState.StartTravel));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            match = match.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapMovementPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }
            var newExactInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Call, extraExactCall),
            };
            match = match.Insert(newExactInstructions);
            Main.GetLogger<GlobalMapMovementPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessageVM), nameof(GlobalMapEnterMessageVM.Accept))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapEnterMessageVM_Accept_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraExactCall = AccessTools.Method(typeof(GlobalMapMovementPatches), nameof(GlobalMapMovementPatches.OnGlobalMapExactMovement));
            var lookFor = AccessTools.Method(typeof(GlobalMapArmyState), nameof(GlobalMapArmyState.StartTravel));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            match = match.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapMovementPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }
            var newExactInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 4),
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Call, extraExactCall),
            };
            match = match.Insert(newExactInstructions);
            Main.GetLogger<GlobalMapMovementPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(NavigationArrowView), nameof(NavigationArrowView.OnClick))]
        [HarmonyPrefix]
        public static void NavigationArrowsController_OnClick_Prefix(NavigationArrowView __instance)
        {
            if (!Main.Multiplayer.IsActive || Game.Instance.CutsceneLock.Active || Game.Instance.CurrentMode == GameModeType.Rest)
            {
                return;
            }

            var travel = CreateGlobalMapTravel(NetworkGlobalMapPathType.Direction, __instance.m_DirLoc.Blueprint, fromClick: true);
            Main.Multiplayer.OnGlobalMapTravelStarted(travel);
        }

        [HarmonyPatch(typeof(GlobalMapView), nameof(GlobalMapView.EnterLocation))]
        [HarmonyPrefix]
        public static void GlobalMapView_EnterLocation_Prefix(GlobalMapView __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.State.Player.Location == null)
            {
                return;
            }

            var location = GetNetworkGlobalMapLocation(__instance.State.Player.Location);
            Main.Multiplayer.OnGlobalMapEnterLocation(location);
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessagePCView), nameof(GlobalMapEnterMessagePCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapEnterMessagePCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(GlobalMapMovementPatches), nameof(GlobalMapMovementPatches.SubscribeEnterMessageEscPress));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapControlPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match = match.Advance(-7)
                .RemoveInstructions(8)
                .Insert(newInstructions);

            Main.GetLogger<GlobalMapControlPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessagePCView), nameof(GlobalMapEnterMessagePCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void GlobalMapEnterMessagePCView_BindViewImplementation_Postfix(GlobalMapEnterMessagePCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            // responsible for hiding message box when you click somewhere else
            __instance.m_VeilButton.Interactable = false;

            Main.Multiplayer.OnGlobalMapMessageBoxShown();
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessageVM), nameof(GlobalMapEnterMessageVM.Close))]
        [HarmonyPrefix]
        public static void GlobalMapEnterMessageVM_Close_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapLocationMessageClosed();
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessageVM), nameof(GlobalMapEnterMessageVM.CanLocationSelect))]
        [HarmonyPostfix]
        public static void GlobalMapEnterMessageVM_CanLocationSelect_Prefix(GlobalMapPointView locationView, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var location = GetNetworkGlobalMapLocation(locationView.Blueprint);
            var canSelectLocation = Main.Multiplayer.OnGlobalMapSelectLocation(location);
            __result = __result && canSelectLocation;
        }

        [HarmonyPatch(typeof(GlobalMapUI), nameof(GlobalMapUI.Awake))]
        [HarmonyPrefix]
        public static void GlobalMapUI_Awake_Prefix(GlobalMapUI __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canNavigate = Main.Multiplayer.CanNavigateOnGlobalMap();
            __instance.m_BtnContinue.GetComponentInChildren<OwlcatButton>().Interactable = canNavigate;
            __instance.m_BtnStop.GetComponentInChildren<OwlcatButton>().Interactable = canNavigate;
        }

        [HarmonyPatch(typeof(GlobalMapUI), nameof(GlobalMapUI.OnContinue))]
        [HarmonyPrefix]
        public static void GlobalMapUI_OnContinue_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var traveler = GetGlobalMapTraveler(Game.Instance.GlobalMapController.SelectedTraveler);
            Main.Multiplayer.OnGlobalMapContinueTravel(traveler);
        }

        [HarmonyPatch(typeof(GlobalMapUI), nameof(GlobalMapUI.OnStop))]
        [HarmonyPrefix]
        public static void GlobalMapUI_OnStop_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var traveler = GetGlobalMapTraveler(Game.Instance.GlobalMapController.SelectedTraveler);
            Main.Multiplayer.OnGlobalMapStopTravel(traveler);
        }

        [HarmonyPatch(typeof(GlobalMapMovementUtility), nameof(GlobalMapMovementUtility.ShowCollectIngredientMessage))]
        [HarmonyPrefix]
        public static bool GlobalMapMovementUtility_ShowCollectIngredientMessage_Prefix(IGlobalMapTraveler traveler, GlobalMapPointState pointState)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // looks super weird to use different approach to show message box confirmation for ingredient collection, but it is what it is
            // slightly modified copy paste of the original method since creating transpiler for compiler generated classes is way worse
            var craftRoot = BlueprintRoot.Instance.CraftRoot.CollectRoot;
            if (pointState.IngredientWasCollected)
            {
                // this one doesn't make sense to sync
                // UIUtility.ShowMessageBox(craftRoot.AlreadyCollected, MessageModalBase.ModalType.Message, null);
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(craftRoot.AlreadyCollected, false), true);
                return false;
            }

            var popup = new NetworkGlobalMapCommonPopup
            {
                Type = NetworkGlobalMapCommonPopupType.Ingredients,
                Location = GetNetworkGlobalMapLocation(traveler.Location)
            };
            Main.Multiplayer.OnGlobalMapCommonPopupShown(popup);
            UIUtility.ShowMessageBox(craftRoot.PointResources, MessageModalBase.ModalType.Dialog, delegate (MessageModalBase.ButtonType result)
            {
                if (result != MessageModalBase.ButtonType.Yes)
                {
                    Main.Multiplayer.OnGlobalMapCommonPopupDeclined(popup);
                    return;
                }

                var collected = craftRoot.CollectIngredient(traveler.Location);
                var warningMessage = collected.Count > 0 ? craftRoot.SuccessCollect : craftRoot.FailCollected;
                UIUtility.SendWarning(warningMessage, addLog: false);
                EventBus.RaiseEvent<ILogMessageUIHandler>(x => x.HandleLogMessage((collected.Count > 0) ? $"{craftRoot.SuccessCollect}:\n{BlueprintGlobalMapPoint.IngredientToString(collected)}" : ((string)craftRoot.FailCollected)));
                pointState.IngredientWasCollected = true;
                pointState.SetVisited();

                Main.Multiplayer.OnGlobalMapCommonPopupAccepted(popup);
            }, null, 0, UIStrings.Instance.Tooltips.Collect, null, [.. traveler.Location.Ingredients.Select(i => i.Ingredient.Get())]);

            return false;
        }

        [HarmonyPatch(typeof(NavigationArrowsController), nameof(NavigationArrowsController.TrySet))]
        [HarmonyPrefix]
        public static bool NavigationArrowsController_TrySet_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var shouldContinue = Main.Multiplayer.CanNavigateOnGlobalMap();
            return shouldContinue;
        }

        private static void OnGlobalMapDirectionalMovement(GlobalMapTravelData globalMapTravelData)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var travel = CreateGlobalMapTravel(NetworkGlobalMapPathType.Direction, globalMapTravelData.To.Location, fromClick: true);
            Main.Multiplayer.OnGlobalMapTravelStarted(travel);
        }

        private static void OnGlobalMapExactMovement(GlobalMapTravelData globalMapTravelData, bool fromClick)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var travel = CreateGlobalMapTravel(NetworkGlobalMapPathType.Exact, globalMapTravelData.To.Location, fromClick);
            Main.Multiplayer.OnGlobalMapTravelStarted(travel);
        }

        private static NetworkGlobalMapTravel CreateGlobalMapTravel(NetworkGlobalMapPathType pathType, BlueprintGlobalMapPoint globalMapPoint, bool fromClick)
        {
            var travel = new NetworkGlobalMapTravel
            {
                Traveler = GetGlobalMapTraveler(Game.Instance.GlobalMapController.SelectedTraveler),
                Destination = GetNetworkGlobalMapLocation(globalMapPoint),
                Type = pathType,
                FromClick = fromClick
            };
            return travel;
        }

        private static IDisposable SubscribeEnterMessageEscPress(GlobalMapEnterMessagePCView view)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Game.Instance.UI.EscManager.Subscribe(view.ViewModel.Close);
            }

            var subscription = Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!view.m_DeclineButton.Interactable)
                {
                    return;
                }

                view.ViewModel.Close();
            });

            return subscription;
        }

        private static NetworkGlobalMapTraveler GetGlobalMapTraveler(IGlobalMapTraveler globalMapTraveler)
        {
            if (globalMapTraveler == null)
            {
                return null;
            }

            var traveler = new NetworkGlobalMapTraveler
            {
                Position = GetGlobalMapPosition(globalMapTraveler.Position),
                MovementPoints = globalMapTraveler is GlobalMapArmyState armyState ? armyState.MovementPoints : null
            };
            return traveler;
        }

        private static NetworkGlobalMapPosition GetGlobalMapPosition(GlobalMapPosition globalMapPosition)
        {
            if (globalMapPosition == null)
            {
                return null;
            }

            var position = new NetworkGlobalMapPosition
            {
                EdgePosition = globalMapPosition.EdgePosition
            };
            return position;
        }

        private static NetworkGlobalMapLocation GetNetworkGlobalMapLocation(BlueprintGlobalMapPoint globalMapPoint)
        {
            var location = new NetworkGlobalMapLocation
            {
                Id = globalMapPoint.AssetGuid.ToString(),
                Name = globalMapPoint.name,
            };

            return location;
        }

        private static void OnFatigueMessageActionChoosen(MessageModalBase.ButtonType btn)
        {
            var popup = new NetworkGlobalMapCommonPopup { Type = NetworkGlobalMapCommonPopupType.Fatigue };
            if (btn == MessageModalBase.ButtonType.Yes)
            {
                Main.Multiplayer.OnGlobalMapCommonPopupAccepted(popup);
                return;
            }

            Main.Multiplayer.OnGlobalMapCommonPopupDeclined(popup);
        }

        private static void OnFatiguePopupShown()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var popup = new NetworkGlobalMapCommonPopup { Type = NetworkGlobalMapCommonPopupType.Fatigue };
            Main.Multiplayer.OnGlobalMapCommonPopupShown(popup);
        }

        /// <summary>
        /// 1. loading fields via IL instructions requires to get type of anonymous class, but it's a really bad idea to have strict dependency on that anonymous compiler-generated class
        /// 2. 'dynamic' requires an extra dlls to be addeded and loaded by the game
        /// All options above don't look likeable enough, so sticking to this temu 'dynamic at home' anonymous class representation for now
        /// </summary>
        private class CompilerGeneratedFleeMessageBoxData
        {
            // order must match order of the anonymous class fields
            public GlobalMapController controller = null;
            public TacticalCombatResults prediction = null;
            public GlobalMapArmyState attacker = null;
            public GlobalMapArmyState defender = null;

            public bool IsValid()
            {
                return controller is not null and GlobalMapController
                    && prediction is not null and TacticalCombatResults
                    && attacker is not null and GlobalMapArmyState
                    && defender is not null and GlobalMapArmyState
                    && attacker != defender;
            }
        }

        /// <summary>
        /// modified copy-paste of GlobalMapController.BeginCombat (flee message box section)
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="fleeMessageBoxData"></param>
        private static void OnShowFleeMessageBox(CompilerGeneratedFleeMessageBoxData fleeMessageBoxData)
        {
            if (!(fleeMessageBoxData?.IsValid() ?? false))
            {
                Main.GetLogger<GlobalMapMovementPatches>().LogError("Invalid compiler-generated flee message box data supplied.");
                return;
            }

            GlobalMapController controller = fleeMessageBoxData.controller;
            GlobalMapArmyState attacker = fleeMessageBoxData.attacker;
            GlobalMapArmyState defender = fleeMessageBoxData.defender;
            TacticalCombatResults prediction = fleeMessageBoxData.prediction;

            var popup = new NetworkGlobalMapCommonPopup { Type = NetworkGlobalMapCommonPopupType.Flee };
            Main.Multiplayer.OnGlobalMapCommonPopupShown(popup);
            UIUtility.ShowMessageBox(UIStrings.Instance.CrusadeTexts.EnemyFleeText, MessageModalBase.ModalType.Dialog, delegate (MessageModalBase.ButtonType result)
            {
                if (result == MessageModalBase.ButtonType.Yes)
                {
                    Main.Multiplayer.OnGlobalMapCommonPopupAccepted(popup);
                    controller.OpenAutoBattleResults(prediction, attacker, defender);
                    return;
                }

                Main.Multiplayer.OnGlobalMapCommonPopupDeclined(popup);
                prediction.Units?.Clear();
                prediction.ToResurrect?.Clear();
                controller.StartManualCombat(attacker, defender);
            }, null, 0, UIStrings.Instance.CrusadeTexts.EnemyFleeAccept, UIStrings.Instance.CrusadeTexts.EnemyFleeDecline, null);
        }
    }
}
