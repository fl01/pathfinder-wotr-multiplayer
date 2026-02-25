using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.MapObjects;
using Kingmaker.Designers;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.MapObjects;

namespace WOTRMultiplayer.HarmonyPatches.Inspect
{
    [HarmonyPatch]
    public class PerceptionPatches
    {
        [HarmonyPatch(typeof(PartyPerceptionController), nameof(PartyPerceptionController.RollPerception))]
        [HarmonyPrefix]
        public static bool PartyPerceptionController_RollPerception_Prefix(UnitEntityData character, StaticEntityData data)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var shouldContinue = Main.Multiplayer.CanMakePerceptionCheck(character.UniqueId, data.UniqueId);
            return shouldContinue;
        }

        [HarmonyPatch(typeof(PartyPerceptionController), nameof(PartyPerceptionController.RollPerception))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PartyPerceptionController_RollPerception_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(PerceptionPatches), nameof(PerceptionPatches.OnPerceptionCheck));
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.Method(typeof(GameHelper), nameof(GameHelper.TriggerSkillCheck));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<PerceptionPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldloc_2),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(2).Insert(newInstructions);
            Main.GetLogger<PerceptionPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void OnPerceptionCheck(UnitEntityData character, StaticEntityData data, RuleSkillCheck ruleSkillCheck)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var check = new NetworkPerceptionCheck
            {
                UnitId = character.UniqueId,
                Roll = ruleSkillCheck.D20.m_Result,
                MapObject = Main.Mapper.Map<NetworkMapObject>(data)
            };

            Main.Multiplayer.OnPerceptionCheck(check);
        }
    }
}
