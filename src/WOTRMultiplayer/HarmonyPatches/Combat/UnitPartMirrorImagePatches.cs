using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.UnitLogic.Parts;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitPartMirrorImagePatches
    {
        [HarmonyPatch(typeof(UnitPartMirrorImage), nameof(UnitPartMirrorImage.TryAbsorbHit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitPartMirrorImage_TryAbsorbHit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(UnitPartMirrorImagePatches), nameof(UnitPartMirrorImagePatches.GetAbsorbedImageCount));
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<UnitPartMirrorImagePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);

            Main.GetLogger<UnitPartMirrorImagePatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int GetAbsorbedImageCount(int minRange, int maxRange, UnitPartMirrorImage image)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minRange, maxRange);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{image.Source.Owner.Unit.UniqueId}:{nameof(UnitPartMirrorImage)}.{nameof(UnitPartMirrorImage.TryAbsorbHit)}_{seededContext.Id}";
                int absorbedCount = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minRange, maxRange);
                Main.GetLogger<UnitPartMirrorImagePatches>().LogInformation("Mirror image absorbtion count has been generated. Id={Id}, Count={Count}, MinRange={MinRange}, MaxRange={MaxRange}", identifier, absorbedCount, minRange, maxRange);

                return absorbedCount;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<UnitPartMirrorImagePatches>().LogError(ex, "Unable to generate mirror image absorbtion count");
                return UnityEngine.Random.Range(minRange, maxRange);
            }
        }
    }
}
