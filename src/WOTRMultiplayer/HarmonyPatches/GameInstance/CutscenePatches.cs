using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.GameInstance
{
    [HarmonyPatch]
    public class CutscenePatches
    {
        [HarmonyPatch(typeof(Game), nameof(Game.SkipCutscene))]
        [HarmonyPrefix]
        public static void Game_SkipCutscene_Prefix(out CutsceneSkipState __state)
        {
            if (!Main.Multiplayer.IsActive)
            {
                __state = null;
                return;
            }

            __state = new CutsceneSkipState
            {
                ShouldStartSkipping = CutsceneController.s_ShouldStartSkipping,
                Skipping = CutsceneController.Skipping
            };
        }

        [HarmonyPatch(typeof(Game), nameof(Game.SkipCutscene))]
        [HarmonyPostfix]
        public static void Game_SkipCutscene_Postfix(CutsceneSkipState __state)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            // player can spam skip (enter keypress) which should be filtered to avoid spamming network messages
            if (!__state.ShouldStartSkipping && CutsceneController.s_ShouldStartSkipping || !__state.Skipping && CutsceneController.Skipping)
            {
                Main.Multiplayer.OnCutsceneSkip();
            }
        }


        public class CutsceneSkipState
        {
            public bool ShouldStartSkipping { get; set; }

            public bool Skipping { get; set; }
        }
    }
}
