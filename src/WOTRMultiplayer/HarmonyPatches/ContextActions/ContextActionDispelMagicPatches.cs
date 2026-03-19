using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.ContextActions
{
    [HarmonyPatch]
    public class ContextActionDispelMagicPatches
    {
        [HarmonyPatch(typeof(ContextActionDispelMagic), nameof(ContextActionDispelMagic.RunAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ContextActionDispelMagic_RunAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(ContextActionDispelMagic), nameof(ContextActionDispelMagic.TryDispelBuff));
            var replaceWith = AccessTools.Method(typeof(ContextActionDispelMagicPatches), nameof(ContextActionDispelMagicPatches.SortBuffsToDispel));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<ContextActionDispelMagicPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-18).RemoveInstructions(10).Insert(newInstructions);
            Main.GetLogger<ContextActionDispelMagicPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        /// <summary>
        /// Additional sorting to not relay on initial order of same DC buffs
        /// </summary>
        /// <param name="buffs"></param>
        private static void SortBuffsToDispel(List<Buff> buffs)
        {
            if (!Main.Multiplayer.IsActive)
            {
                buffs.Sort((b1, b2) => -b1.Context.Params.CasterLevel.CompareTo(b2.Context.Params.CasterLevel));
                return;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                buffs.Sort((buff1, buff2) =>
                {
                    int result = -buff1.Context.Params.CasterLevel.CompareTo(buff2.Context.Params.CasterLevel);
                    if (result != 0)
                    {
                        return result;
                    }

                    var buff1Identifier = seededContext.Id + buff1.Blueprint?.name;
                    var buff2Identifier = seededContext.Id + buff2.Blueprint?.name;
                    var buff1Seed = Main.HashService.Murmur3(buff1Identifier);
                    var buff2Seed = Main.HashService.Murmur3(buff2Identifier);

                    return buff1Seed.CompareTo(buff2Seed);
                });
            }
            catch (Exception ex)
            {
                Main.GetLogger<ContextActionDispelMagicPatches>().LogError(ex, "Error while sorting buffs for dispel rule");
                throw;
            }
        }
    }
}
