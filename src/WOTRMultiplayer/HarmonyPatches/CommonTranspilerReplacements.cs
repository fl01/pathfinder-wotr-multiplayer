using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches
{
    public class CommonTranspilerReplacements
    {
        public static void ReplaceIsDirectlyControllableWithLocalPlayerCheck(CodeMatcher matcher, string target, bool withLabels = false, bool fromEnd = false)
        {
            var replaceWith = AccessTools.Method(typeof(CommonTranspilerReplacements), nameof(CommonTranspilerReplacements.IsControlledByLocalPlayer));
            var lookFor = AccessTools.PropertyGetter(typeof(UnitEntityData), nameof(UnitEntityData.IsDirectlyControllable));
            var match = fromEnd ? matcher.End().SearchBackwards(x => x.Calls(lookFor)) : matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<CommonTranspilerReplacements>().LogError("Transpiler has not been applied. Target={Target}", target);
                return;
            }

            var call = new CodeInstruction(OpCodes.Call, replaceWith);
            if (withLabels)
            {
                var labels = match.Instruction.ExtractLabels();
                call = call.WithLabels(labels);
            }

            match.RemoveInstruction();
            match.Insert(call);

            Main.GetLogger<CommonTranspilerReplacements>().LogInformation("Transpiler has been applied. Target={Target}", target);
        }

        private static bool IsControlledByLocalPlayer(UnitEntityData unit)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return unit.IsDirectlyControllable;
                }

                return unit != null && unit.IsDirectlyControllable && Main.Multiplayer.IsControlledByLocalPlayer(unit.UniqueId);
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<CommonTranspilerReplacements>().LogError(ex, "Failed to check if controlled by players. UnitId={unitId}", unit?.UniqueId);
                throw;
            }
        }
    }
}
