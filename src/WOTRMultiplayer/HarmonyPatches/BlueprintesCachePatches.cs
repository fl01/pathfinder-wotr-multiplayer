using System.Reflection;
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
            Main.GetLogger<BlueprintesCachePatches>().LogInformation("Applying patch. MethodName={MethodName}", MethodBase.GetCurrentMethod().Name);

            Main.InitializePortraits();
        }
    }
}
