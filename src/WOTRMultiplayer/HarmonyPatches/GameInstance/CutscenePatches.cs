using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.GameInstance
{
    [HarmonyPatch]
    public class CutscenePatches
    {
        [HarmonyPatch(typeof(Game), nameof(Game.SkipCutscene))]
        [HarmonyPostfix]
        public static void Game_SkipCutscene_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (CutsceneController.s_ShouldStartSkipping || CutsceneController.Skipping)
            {
                Main.Multiplayer.OnCutsceneSkip();
            }
        }
    }
}
