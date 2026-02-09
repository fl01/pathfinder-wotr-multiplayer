using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Message;
using Kingmaker.UI.MVVM._VM.GlobalMap.Message;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class GlobalMapRandomEncounterPatches
    {
        [HarmonyPatch(typeof(GlobalMapRandomEncounterView), nameof(GlobalMapRandomEncounterView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapRandomEncounterView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(GlobalMapRandomEncounterView), nameof(GlobalMapRandomEncounterView.EnterEncounterAfterDelayCoroutine));
            var replaceWith = AccessTools.Method(typeof(GlobalMapRandomEncounterPatches), nameof(GlobalMapRandomEncounterPatches.SetupAutoEnterEncounter));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<LeaderLevelUpPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-2).RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<LeaderLevelUpPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void SetupAutoEnterEncounter(GlobalMapRandomEncounterView view)
        {
            if (!Main.Multiplayer.IsActive)
            {
                view.m_AutoAcceptCoroutine = CoroutineRunner.Start(view.EnterEncounterAfterDelayCoroutine());
            }
        }

        [HarmonyPatch(typeof(GlobalMapRandomEncounterPCView), nameof(GlobalMapRandomEncounterPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void GlobalMapRandomEncounterPCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapEncounterMessageShown();
        }

        [HarmonyPatch(typeof(GlobalMapRandomEncounterView), nameof(GlobalMapRandomEncounterView.AcceptAndStopCoroutine))]
        [HarmonyPrefix]
        public static void GlobalMapRandomEncounterView_AcceptAndStopCoroutine_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapEncounterAccepted();
        }

        [HarmonyPatch(typeof(GlobalMapRandomEncounterVM), nameof(GlobalMapRandomEncounterVM.Avoid))]
        [HarmonyPrefix]
        public static void GlobalMapRandomEncounterVM_Avoid_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapEncounterAvoided();
        }

        [HarmonyPatch(typeof(RandomEncountersController), nameof(RandomEncountersController.RollTravelEncounter))]
        [HarmonyPrefix]
        public static bool RandomEncountersController_RollTravelEncounter_Prefix(ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnGlobalMapBeforeRollTravelEncounter();
            if (!canContinue)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(RandomEncountersController), nameof(RandomEncountersController.RollTravelEncounter))]
        [HarmonyPostfix]
        public static void RandomEncountersController_RollTravelEncounter_Postfix(ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || !__result)
            {
                return;
            }

            var encounter = RandomEncountersController.State.Player.CurrentEncounterData;
            var randomEncounter = new NetworkGlobalMapEncounter
            {
                AvoidanceResult = encounter.AvoidanceCheckResult.ToString(),
                BlueprintId = encounter.Blueprint.AssetGuid.ToString(),
                Position = encounter.Position?.ToNetworkVector3(),
                Seed = encounter.RandomCombat.Seed,
                IsTrader = encounter.IsTraderRE,
            };

            Main.Multiplayer.OnGlobalMapEncounterRolled(randomEncounter);
        }
    }
}
