using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Microsoft.Extensions.Logging;

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
            Main.GetLogger<ContextActionAddRandomTrashItemPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
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
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(ContextActionAddRandomTrashItem)}:{nameof(GetContextActionLootRandomizator)}:{contextActionAddRandom.m_LootType}:{contextActionAddRandom.m_MaxCost}:{contextActionAddRandom.m_Silent}_{seededContext.Id}";
                var randomizer = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
                Main.GetLogger<ContextActionAddRandomTrashItemPatches>().LogInformation("Randomizer for ContextActionAddRandomTrashItem has been initialized. Identifier={Identifier}", identifier);
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
