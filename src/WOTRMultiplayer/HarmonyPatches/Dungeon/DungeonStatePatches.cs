using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Dungeon;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.HarmonyPatches.Dialogs;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Dungeon
{
    [HarmonyPatch]
    public class DungeonStatePatches
    {
        [HarmonyPatch(typeof(DungeonState), nameof(DungeonState.Seed), MethodType.Getter)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> DungeonState_Seed_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.Method(typeof(Guid), nameof(Guid.NewGuid));
            var replaceWith = AccessTools.Method(typeof(DungeonStatePatches), nameof(DungeonStatePatches.GenerateDungeonSeed));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<DungeonStatePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith)
            };
            match = match.RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<DungeonStatePatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int GenerateDungeonSeed()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Guid.NewGuid().GetHashCode();
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(DungeonState)}:{nameof(GenerateDungeonSeed)}_{seededContext.Id}";
                int dungeonSeed = Main.Multiplayer.ValueGenerator.GetRandom(IdentifierLifetime.Persistent, identifier).Next();
                Main.GetLogger<CueSelectionPatches>().LogInformation("Dungeon Seed has been generated. DungeonSeed={DungeonSeed}, Identifier={Identifier}", dungeonSeed, identifier);
                return dungeonSeed;
            }
            catch (Exception ex)
            {
                Main.GetLogger<CueSelectionPatches>().LogError(ex, "Unable to select dialog cue");
                throw;
            }
        }
    }
}
