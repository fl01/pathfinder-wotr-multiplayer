using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Combat;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitCombatPrepareControllerPatches
    {
        [HarmonyPatch(typeof(UnitCombatPrepareController), nameof(UnitCombatPrepareController.Tick))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitCombatPrepareController_Tick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.PropertySetter(typeof(UnitCombatState), nameof(UnitCombatState.InitiativeRandom));
            var replaceWith = AccessTools.Method(typeof(UnitCombatPrepareControllerPatches), nameof(UnitCombatPrepareControllerPatches.OnRollRandomInitiative));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitCombatPrepareControllerPatches>().LogError("Unable to find first random initiative replacement. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>
            {
                new (OpCodes.Call, replaceWith)
            };
            match = match.Advance(-2).RemoveInstructions(2).Insert(newInstructions);
            match = match.Advance(newInstructions.Count + 1);

            match = match.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitCombatPrepareControllerPatches>().LogError("Unable to find second random initiative replacement. Target={Target}", target);
                return instructions;
            }
            match = match.Advance(-2).RemoveInstructions(2).Insert(newInstructions);
            Main.GetLogger<UnitCombatPrepareControllerPatches>().LogDebug("Transpiler has been applied (x2). Target={Target}", target);
            return matcher.Instructions();
        }

        private static int OnRollRandomInitiative(RuleRollD20 ruleRollD20)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Game.Instance.Rulebook.TriggerEvent(ruleRollD20).Result;
            }

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var combatSeed = Main.Multiplayer.GetCombatSeed();
                var combatTurnSeed = Main.Multiplayer.GetCombatTurnSeed();
                var crusadeArmySeed = Main.Multiplayer.GetCrusadeArmyCombatSeed();

                var identifier = $"{nameof(UnitCombatPrepareController)}:{nameof(OnRollRandomInitiative)}:{ruleRollD20.Initiator.UniqueId}_{sessionSeed}:{combatSeed}:{combatTurnSeed}:{crusadeArmySeed}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(IdentifierLifetime.Combat, identifier);
                var randomInitiative = ruleRollD20.DiceFormula.Roll(random);
                Main.GetLogger<UnitCombatPrepareControllerPatches>().LogInformation("Random initiative has been rolled. UnitId={UnitId}, Initiative={Initiative}, Identifier={Identifier}", ruleRollD20.Initiator.UniqueId, randomInitiative, identifier);
                return randomInitiative;
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitCombatPrepareControllerPatches>().LogError(ex, "Unable to roll unit random initiative. UnitId={UnitId}", ruleRollD20.Initiator.UniqueId);
                throw;
            }
        }
    }
}
