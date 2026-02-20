using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.ContextActions
{
    [HarmonyPatch]
    public class ContextActionAddRandomTrashItemPatches
    {
        [HarmonyPatch(typeof(ContextActionAddRandomTrashItem), nameof(ContextActionAddRandomTrashItem.RunAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ContextActionAddRandomTrashItem_RunAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWith = AccessTools.Method(typeof(ContextActionAddRandomTrashItemPatches), nameof(ContextActionAddRandomTrashItemPatches.GetContextActionLootRandomizator));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Is(OpCodes.Ldftn, lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<ContextActionAddRandomTrashItemPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(2).Insert(newInstructions);
            Main.GetLogger<ContextActionAddRandomTrashItemPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static Func<int, int, int> GetContextActionLootRandomizator(ContextActionAddRandomTrashItem contextActionAddRandom)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range;
            }

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSaveSeed = Main.Multiplayer.GetLoadedSaveSeed();

                var identifier = $"{nameof(ContextActionAddRandomTrashItem)}:{nameof(GetContextActionLootRandomizator)}:{Game.Instance.Player.GameId}:{contextActionAddRandom.m_LootType}:{contextActionAddRandom.m_MaxCost}:{contextActionAddRandom.m_Silent}:{sessionSeed}:{loadedSaveSeed}";
                var randomizer = Main.Multiplayer.ValueGenerator.GetRandom(IdentifierLifetime.Area, identifier);
                Main.GetLogger<ContextActionAddRandomTrashItemPatches>().LogInformation("Randomizer for ContextActionAddRandomTrashItem has been initialized. Identifier={Identifier}, SessionSeed={SessionSeed}, LoadedSaveSeed={LoadedSaveSeed}", identifier, sessionSeed, loadedSaveSeed);
                return randomizer.Next;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ContextActionAddRandomTrashItemPatches>().LogError(ex, "Error while initializing randomizer for ContextActionAddRandomTrashItem");
                throw;
            }
        }
    }
}
