using System;

namespace WOTRMultiplayer.HarmonyPatches
{
    public static class PatchesUtils
    {
        public static bool IsHelperUnit(string unitId)
        {
            return !string.IsNullOrEmpty(unitId) && unitId.StartsWith("description-helper-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
