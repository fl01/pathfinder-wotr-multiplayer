using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace WOTRMultiplayer.HarmonyPatches.Rest
{
    [HarmonyPatch]
    public class RestPartPatches
    {
        /// <summary>
        /// Can't patch Player.GetPartyCharactersForGroupCommand directly since it's default behavior is fine for other cases
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(RestPart), nameof(RestPart.OnInteract))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PlaceRestMarkerHandler_OnClick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RestPartPatches), nameof(GetPartyCharactersForGroupCommand));
            var lookFor = AccessTools.Method(typeof(Player), nameof(Player.GetPartyCharactersForGroupCommand));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestPartPatches>().LogError("Invalid transpiler position. Target={target}, Pos={pos}", target, match.Pos);
                return instructions;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<RestPartPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

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
    }
}
