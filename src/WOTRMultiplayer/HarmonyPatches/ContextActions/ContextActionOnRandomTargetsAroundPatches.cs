using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.ContextActions
{
    [HarmonyPatch]
    public class ContextActionOnRandomTargetsAroundPatches
    {
        [HarmonyPatch(typeof(ContextActionOnRandomTargetsAround), nameof(ContextActionOnRandomTargetsAround.RunAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ContextActionOnRandomTargetsAround_RunAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(ContextActionOnRandomTargetsAroundPatches), nameof(ContextActionOnRandomTargetsAroundPatches.SelectRandomTarget));
            var matcher = new CodeMatcher(instructions);
            // TODO: need to check how to properly find a call to generic extension method (LinqExtensions.Random) with few overloads. Sticking to shitty lookup for now :/
            var lookFor = $"{typeof(UnitEntityData).FullName} {nameof(LinqExtensions.Random)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));

            if (match.IsInvalid)
            {
                Main.GetLogger<ContextActionOnRandomTargetsAroundPatches>().LogError("Invalid transpiler position. Target={Target}", target);
                return matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };

            match = match.RemoveInstruction().Insert(newInstructions);

            Main.GetLogger<ContextActionOnRandomTargetsAroundPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static UnitEntityData SelectRandomTarget(IList<UnitEntityData> targets, ContextActionOnRandomTargetsAround contextActionOnRandomTargetsAround)
        {
            if (!Main.Multiplayer.IsActive || targets == null || targets.Count == 0)
            {
                return targets.Random();
            }

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSave = Main.Multiplayer.GetLoadedSaveSeed();
                var areaSeed = Main.Multiplayer.GetAreaSeed();
                var combatSeed = Main.Multiplayer.GetCombatSeed();
                var combatTurnSeed = Main.Multiplayer.GetCombatTurnSeed();
                var armyCombatSeed = Main.Multiplayer.GetCrusadeArmyCombatSeed();

                var unitId = contextActionOnRandomTargetsAround.AbilityContext.MaybeOwner?.UniqueId;
                var targetId = contextActionOnRandomTargetsAround.AbilityContext.MainTarget?.Unit?.UniqueId;
                var abilityName = contextActionOnRandomTargetsAround.AbilityContext.NameForAcronym;
                var attackNumber = contextActionOnRandomTargetsAround.AbilityContext.AttackRoll?.RuleAttackWithWeapon?.AttackNumber ?? -1;
                var units = string.Join(",", targets.Select(x => x.UniqueId));
                var lifetime = combatSeed == 0 ? IdentifierLifetime.Area : IdentifierLifetime.CombatTurn;
                var identifier = $"{nameof(ContextActionOnRandomTargetsAround)}:{nameof(SelectRandomTarget)}:{units}:{unitId}:{targetId}:{abilityName}:{attackNumber}_{sessionSeed}:{loadedSave}:{areaSeed}:{combatSeed}:{combatTurnSeed}:{armyCombatSeed}";
                var index = Main.Multiplayer.ValueGenerator.Range(lifetime, identifier, 0, targets.Count);
                var unit = targets[index];
                Main.GetLogger<ContextActionRandomizePatches>().LogInformation("ContextActionOnRandomTargetsAround has been rolled. UnitId={UnitId}, Lifetime={Lifetime}, Identifier={Identifier}", unitId, lifetime, identifier);
                return unit;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ContextActionRandomizePatches>().LogError(ex, "Error while rolling random action");
                throw;
            }
        }
    }
}
