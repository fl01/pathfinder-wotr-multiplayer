using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.State;
using Kingmaker.Blueprints;
using Kingmaker.Kingdom.Armies;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._PCView.Crusade.Recruit;
using Kingmaker.UI.MVVM._VM.Crusade.Recruit;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class RecruitViewPatches
    {
        [HarmonyPatch(typeof(RecruitPCView), nameof(RecruitPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RecruitPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(RecruitViewPatches), nameof(RecruitViewPatches.SubscribeEnterMessageEscPress));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RecruitViewPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(1).Insert(newInstructions);
            Main.GetLogger<RecruitViewPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RecruitPCView), nameof(RecruitPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void RecruitPCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapRecruitmentShown();
        }

        [HarmonyPatch(typeof(RecruitVM), nameof(RecruitVM.HandleSlotsRerolled))]
        [HarmonyPostfix]
        public static void RecruitVM_HandleSlotsRerolled_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapRecruitmentSlotsRerolled();
        }

        [HarmonyPatch(typeof(RecruitView), nameof(RecruitView.OnMercReroll))]
        [HarmonyPrefix]
        public static void RecruitView_OnMercReroll_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapRecruitmentMercReroll();
        }

        [HarmonyPatch(typeof(RecruitVM), nameof(RecruitVM.NextArmy))]
        [HarmonyPrefix]
        public static void RecruitVM_NextArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapRecruitmentNextArmy();
        }

        [HarmonyPatch(typeof(RecruitVM), nameof(RecruitVM.PrevArmy))]
        [HarmonyPrefix]
        public static void RecruitVM_PrevArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapRecruitmentPrevArmy();
        }

        [HarmonyPatch(typeof(RecruitVM), nameof(RecruitVM.Close))]
        [HarmonyPrefix]
        public static void RecruitVM_Close_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapRecruitmentClosed();
        }

        [HarmonyPatch(typeof(RecruitVM), nameof(RecruitVM.BuyRecruit))]
        [HarmonyPrefix]
        public static void RecruitVM_BuyRecruit_Prefix(BlueprintUnit unit, int count)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapUnitRecruitmentOrder = new NetworkGlobalMapUnitRecruitmentOrder
            {
                BlueprintId = unit.AssetGuid.ToString(),
                Count = count,
                Type = NetworkGlobalMapUnitRecruitmentType.Unit,
            };
            Main.Multiplayer.OnGlobalMapRecruitmentBuyUnits(globalMapUnitRecruitmentOrder);
        }

        [HarmonyPatch(typeof(RecruitBuyResourcesVM), nameof(RecruitBuyResourcesVM.Buy))]
        [HarmonyPrefix]
        public static void RecruitBuyResourcesVM_Buy_Prefix(RecruitBuyResourcesVM __instance)
        {
            if (!Main.Multiplayer.IsActive || Game.Instance.Player.Money < __instance.MoneyCount)
            {
                return;
            }

            var globalMapResourceOrder = new NetworkGlobalMapResourceOrder
            {
                FinalCost = __instance.FinalCost.Value,
                MaterialCount = __instance.m_MaterialCount.Value,
                FinanceCount = __instance.m_FinanceCount.Value,
            };
            Main.Multiplayer.OnGlobalMapRecruitmentBuyResources(globalMapResourceOrder);
        }

        [HarmonyPatch(typeof(RecruitView), nameof(RecruitView.CreateArmy))]
        [HarmonyPrefix]
        public static void RecruitPCView_CreateArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCreateCrusadeArmy();
        }

        private IDisposable SubscribeEnterMessageEscPress(Action action, RecruitPCView view)
        {
            return Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || view.m_Close.Interactable)
                {
                    action?.Invoke();
                }
            });
        }
    }
}
