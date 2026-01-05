using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace WOTRMultiplayer.HarmonyPatches
{
    public class CommonTranspilerReplacements
    {
        /// <summary>
        /// Can't patch Player.GetPartyCharactersForGroupCommand directly since its default behavior is fine for other cases
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        public static List<UnitEntityData> GetPartyCharactersForGroupCommand(Vector3 approachPoint, bool skipNoIsInGame)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Game.Instance.Player.GetPartyCharactersForGroupCommand(approachPoint, skipNoIsInGame);
            }

            ObstacleAnalyzer.GetArea(approachPoint);

            var campingUnits = Game.Instance.Player.PartyAndPets
                .Where(u => Main.Multiplayer.IsControlledByPlayers(u.UniqueId))
                .Where(u => u.Descriptor.State.CanMove)
                .Where(u => !u.Descriptor.State.HasCondition(UnitCondition.Paralyzed))
                .Where(u => u.Parts.Get<UnitPartSaddled>() == null)
                .Where(u => u.IsInGame || !skipNoIsInGame)
                .ToList();

            return campingUnits;
        }

        public static void ReplaceIsDirectlyControllable(CodeMatcher matcher, string target, bool withLabels = false, bool fromEnd = false)
        {
            var replaceWith = AccessTools.Method(typeof(CommonTranspilerReplacements), nameof(CommonTranspilerReplacements.IsControlledByPlayers));
            var lookFor = AccessTools.PropertyGetter(typeof(UnitEntityData), nameof(UnitEntityData.IsDirectlyControllable));
            var match = fromEnd ? matcher.End().SearchBackwards(x => x.Calls(lookFor)) : matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<CommonTranspilerReplacements>().LogError("Transpiler has not been applied. Target={Target}", target);
                return;
            }

            var call = new CodeInstruction(OpCodes.Call, replaceWith);
            if (withLabels)
            {
                var labels = match.Instruction.ExtractLabels();
                call = call.WithLabels(labels);
            }

            match.RemoveInstruction();
            match.Insert(call);

            Main.GetLogger<CommonTranspilerReplacements>().LogInformation("Transpiler has been applied. Target={Target}", target);
        }

        public static void ReplaceIsDirectlyControllableWithLocalPlayerCheck(CodeMatcher matcher, string target, bool withLabels = false, bool fromEnd = false)
        {
            var replaceWith = AccessTools.Method(typeof(CommonTranspilerReplacements), nameof(CommonTranspilerReplacements.IsControlledByLocalPlayer));
            var lookFor = AccessTools.PropertyGetter(typeof(UnitEntityData), nameof(UnitEntityData.IsDirectlyControllable));
            var match = fromEnd ? matcher.End().SearchBackwards(x => x.Calls(lookFor)) : matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<CommonTranspilerReplacements>().LogError("Transpiler has not been applied. Target={Target}", target);
                return;
            }

            var call = new CodeInstruction(OpCodes.Call, replaceWith);
            if (withLabels)
            {
                var labels = match.Instruction.ExtractLabels();
                call = call.WithLabels(labels);
            }

            match.RemoveInstruction();
            match.Insert(call);

            Main.GetLogger<CommonTranspilerReplacements>().LogInformation("Transpiler has been applied. Target={Target}", target);
        }

        public static bool IsControlledByPlayers(UnitEntityData unit)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return unit.IsDirectlyControllable;
                }

                return unit.IsDirectlyControllable && Main.Multiplayer.IsControlledByPlayers(unit.UniqueId);
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<CommonTranspilerReplacements>().LogError(ex, "Failed to check if controlled by players. UnitId={unitId}", unit?.UniqueId);
                throw;
            }
        }

        public static bool IsControlledByLocalPlayer(UnitEntityData unit)
        {
            try
            {
                if (!Main.Multiplayer.IsActive)
                {
                    return unit.IsDirectlyControllable;
                }

                return unit.IsDirectlyControllable && Main.Multiplayer.IsControlledByLocalPlayer(unit.UniqueId);
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<CommonTranspilerReplacements>().LogError(ex, "Failed to check if controlled by players. UnitId={unitId}", unit?.UniqueId);
                throw;
            }
        }
    }
}
