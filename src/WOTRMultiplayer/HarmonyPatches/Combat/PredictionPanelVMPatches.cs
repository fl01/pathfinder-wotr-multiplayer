using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI._ConsoleUI.TurnBasedMode;
using Microsoft.Extensions.Logging;
using TurnBased.Controllers;
using UniRx;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class PredictionPanelVMPatches
    {
        [HarmonyPatch(typeof(PredictionPanelVM), MethodType.Constructor, [typeof(UnitEntityData), typeof(BoolReactiveProperty)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PredictionPanelVM_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(PredictionPanelVMPatches), nameof(PredictionPanelVMPatches.TryCatchedSubscription));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.End().Advance(-7);
            if (match.Opcode != OpCodes.Ldarg_0)
            {
                Main.GetLogger<PredictionPanelVMPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match.RemoveInstructions(6);
            var newInstructions = new List<CodeInstruction>()
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<PredictionPanelVMPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static IDisposable TryCatchedSubscription(PredictionPanelVM predictionPanelVM)
        {
            return (MainThreadDispatcher.UpdateAsObservable().Subscribe(delegate (Unit _)
            {
                try
                {
                    ReactiveProperty<bool> isShowAllowed = predictionPanelVM.IsShowAllowed;
                    TurnController turnController = predictionPanelVM.TurnController;
                    isShowAllowed.Value = turnController != null && turnController.SelectedUnit.IsDirectlyControllable;
                }
                catch (NullReferenceException)
                {
                }
            }));
        }
    }
}
