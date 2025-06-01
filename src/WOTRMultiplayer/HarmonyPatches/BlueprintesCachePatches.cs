using HarmonyLib;
using Kingmaker.Blueprints.JsonSystem;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class BlueprintesCachePatches
    {
        [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
        [HarmonyPostfix]
        public static void BlueprintesCachePatches_Init_Postfix()
        {
            var logger = Main.GetLogger<BlueprintesCachePatches>();
            logger.LogInformation("Applying patch [{patchName}]", nameof(BlueprintesCachePatches_Init_Postfix));
            Main.InitializePortraits();
        }
    }
}
