using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Inspect;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Inspect;

namespace WOTRMultiplayer.HarmonyPatches.Inspect
{
    [HarmonyPatch]
    public class InspectUnitsManagerPatches
    {
        [HarmonyPatch(typeof(InspectUnitsManager), nameof(InspectUnitsManager.TryMakeKnowledgeCheck), [typeof(UnitEntityData), typeof(UnitEntityData), typeof(StatType?)])]
        [HarmonyPrefix]
        public static bool InspectUnitsManager_TryMakeKnowledgeCheck_Prefix(UnitEntityData unit, UnitEntityData inspector, StatType? overrideStat = null)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            Main.GetLogger<InspectUnitsManagerPatches>().LogError("TODO: check if second knowledge check must by synced. TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, OverrideStatType={OverrideStatType}", unit?.UniqueId, inspector?.UniqueId, overrideStat);
            return true;
        }

        [HarmonyPatch(typeof(InspectUnitsManager), nameof(InspectUnitsManager.TryMakeKnowledgeCheck), [typeof(UnitEntityData)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> InspectUnitsManager_TryMakeKnowledgeCheck_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(InspectUnitsManagerPatches), nameof(OnInspectionKnowledgeCheck));
            var lookFor = AccessTools.Method(typeof(GameHelper), nameof(GameHelper.TriggerSkillCheck));
            var matcher = new CodeMatcher(instructions);

            var falseStatementPositionLookUp = AccessTools.Method(typeof(InspectUnitsManager.UnitInfo), nameof(InspectUnitsManager.UnitInfo.SetCheck));
            var labels = matcher.Start().SearchForward(x => x.Calls(falseStatementPositionLookUp)).Advance(1).Labels;

            var match = matcher.Start().SearchForward(x => x.Calls(lookFor));
            if (labels.Count == 0 || match.IsInvalid)
            {
                Main.GetLogger<InspectUnitsManagerPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-9);
            var oldLabels = match.Instruction.ExtractLabels();
            var newInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1).WithLabels(oldLabels),
                new(OpCodes.Ldloc_S, 6),
                new(OpCodes.Ldloc_S, 4),
                new(OpCodes.Ldloc_3),
                new(OpCodes.Call, replaceWith),
                new(OpCodes.Brfalse_S, labels.First()),
            };
            match.Insert(newInstructions);

            Main.GetLogger<InspectUnitsManagerPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static bool OnInspectionKnowledgeCheck(UnitEntityData target, UnitEntityData initiator, StatType statType, int dc)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var check = new NetworkInspectionKnowledgeCheck
            {
                TargetUnitId = target.UniqueId,
                InitiatorUnitId = initiator?.UniqueId,
                StatType = statType,
                DC = dc,
                InspectionBlueprintId = target.BlueprintForInspection?.AssetGuid.ToString()
            };
            var canContinue = Main.Multiplayer.OnInspectionKnowledgeCheck(check);
            return canContinue;
        }
    }
}
