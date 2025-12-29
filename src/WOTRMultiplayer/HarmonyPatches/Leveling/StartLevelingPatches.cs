using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._VM.CharGen;
using Kingmaker.UI.MVVM._VM.Party;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.LevelClassScores.Experience;
using Kingmaker.UnitLogic.Class.LevelUp;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class StartLevelingPatches
    {
        [HarmonyPatch(typeof(CharInfoExperienceVM), nameof(CharInfoExperienceVM.LevelUp))]
        [HarmonyPrefix]
        public static bool CharInfoExperienceVM_LevelUp_Prefix(CharInfoExperienceVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.RequestLevelingUI(__instance.Unit.Value.Unit.UniqueId, NetworkLevelingType.Leveling);
            return canContinue;
        }

        [HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.LevelUp))]
        [HarmonyPrefix]
        public static bool PartyCharacterVM_LevelUp_Prefix(PartyCharacterVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.RequestLevelingUI(__instance.UnitEntityData.UniqueId, NetworkLevelingType.Leveling);
            return canContinue;
        }

        [HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.MythicLevelUp))]
        [HarmonyPrefix]
        public static bool PartyCharacterVM_MythicLevelUp_Prefix(PartyCharacterVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.RequestLevelingUI(__instance.UnitEntityData.UniqueId, NetworkLevelingType.MythicLeveling);
            return canContinue;
        }

        [HarmonyPatch(typeof(OpenSelectMythicUI), nameof(OpenSelectMythicUI.RunAction))]
        [HarmonyPrefix]
        public static void OpenSelectMythicUI_RunAction_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var unitId = Game.Instance.Player.MainCharacter.Value.UniqueId;
            Main.Multiplayer.ForceLevelingUI(unitId, NetworkLevelingType.MythicLeveling);
        }

        [HarmonyPatch(typeof(RespecWindowVM), nameof(RespecWindowVM.InitiateNextLevelup))]
        [HarmonyPrefix]
        public static void RespecCompanion_InitiateNextLevelup_Prefix(RespecWindowVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var unitId = __instance.CurrentUnit.Value.UniqueId;
            Main.Multiplayer.ForceLevelingUI(unitId, NetworkLevelingType.Leveling);
        }

        [HarmonyPatch(typeof(RespecWindowVM), nameof(RespecWindowVM.InitiateNextMythic))]
        [HarmonyPrefix]
        public static void RespecCompanion_InitiateNextMythic_Prefix(RespecWindowVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var unitId = __instance.CurrentUnit.Value.UniqueId;
            Main.Multiplayer.ForceLevelingUI(unitId, NetworkLevelingType.MythicLeveling);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.CreateCustomCompanion))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Player_CreateCustomCompanion_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(StartLevelingPatches), nameof(StartLevelingPatches.OnCreateCompanion));
            var lookFor = AccessTools.Method(typeof(LevelUpConfig), nameof(LevelUpConfig.Create));
            var matcher = new CodeMatcher(codeInstructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<StartLevelingPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                matcher.Instructions();
            }

            match = matcher.Advance(-3);
            var labels = matcher.Instruction.ExtractLabels();
            var loadField = match.InstructionAt(1);
            var newInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldloc_0).WithLabels(labels),
                loadField,
                new(OpCodes.Call, extraCall),
                new(OpCodes.Ldloc_0),
                loadField,
            };

            match = matcher.RemoveInstructions(2).Insert(newInstructions);

            Main.GetLogger<StartLevelingPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static void OnCreateCompanion(UnitEntityData unitEntityData)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.ForceLevelingUI(unitEntityData.UniqueId, NetworkLevelingType.Mercenary);
        }
    }
}
