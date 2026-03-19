using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.HarmonyPatches.ContextActions;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap.Kingdom
{
    [HarmonyPatch]
    public class CrusadeEventsTimelinePatches
    {
        [HarmonyPatch(typeof(CrusadeEventsTimeline), nameof(CrusadeEventsTimeline.TryStartEvent))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CrusadeEventsTimeline_TryStartEvent_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookForRandom = $"{typeof(BlueprintCrusadeEvent).FullName} {nameof(LinqExtensions.Random)}";
            var replaceRandom = AccessTools.Method(typeof(CrusadeEventsTimelinePatches), nameof(CrusadeEventsTimelinePatches.RollRandomEvent));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookForRandom) ?? false));
            if (match.IsInvalid)
            {
                Main.GetLogger<CrusadeEventsTimelinePatches>().LogError("Transpiler has not been applied (Random). Target={Target}", target);
                return instructions;
            }

            var randomInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceRandom),
            };
            match.RemoveInstruction().Insert(randomInstructions);

            var lookForNextEventDelay = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceNextEventDelay = AccessTools.Method(typeof(CrusadeEventsTimelinePatches), nameof(CrusadeEventsTimelinePatches.RollNextEventDelay));
            match = match.SearchForward(x => x.Calls(lookForNextEventDelay));
            if (match.IsInvalid)
            {
                Main.GetLogger<CrusadeEventsTimelinePatches>().LogError("Transpiler has not been applied (NextDelay). Target={Target}", target);
                return instructions;
            }
            var nextEventDelayInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceNextEventDelay),
            };
            match.RemoveInstruction().Insert(nextEventDelayInstructions);

            Main.GetLogger<CrusadeEventsTimelinePatches>().LogDebug("Transpiler has been applied (Random + NextDelay). Target={Target}", target);
            return matcher.Instructions();
        }

        private static BlueprintCrusadeEvent RollRandomEvent(IList<BlueprintCrusadeEvent> events, CrusadeEventsTimeline crusadeEventsTimeline)
        {
            if (!Main.Multiplayer.IsActive || events == null || events.Count == 0)
            {
                return events.Random();
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var nextEventStartDay = crusadeEventsTimeline.NextEventStartDay;
                var identifier = $"{nameof(CrusadeEventsTimeline)}:{nameof(RollRandomEvent)}:{nextEventStartDay}_{seededContext.Id}";
                var index = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, 0, events.Count);
                var randomEvent = events[index];
                Main.GetLogger<CrusadeEventsTimelinePatches>().LogInformation("CrusadeEventsTimeline Random event has been rolled. EventId={EventId}, EventName={EventName}, Identifier={Identifier}", randomEvent.AssetGuid.ToString(), randomEvent.name, identifier);
                return randomEvent;
            }
            catch (Exception ex)
            {
                Main.GetLogger<CrusadeEventsTimelinePatches>().LogError(ex, "Error while rolling random crusade timeline event");
                throw;
            }
        }

        private static int RollNextEventDelay(int minInclusive, int maxExclusive, CrusadeEventsTimeline crusadeEventsTimeline)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var eventsCount = crusadeEventsTimeline.m_CurrentEventList.Events.Length;
                var identifier = $"{nameof(CrusadeEventsTimeline)}:{nameof(RollNextEventDelay)}:{eventsCount}:{minInclusive}:{maxExclusive}_{seededContext.Id}";
                var delay = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minInclusive, maxExclusive);
                Main.GetLogger<ContextActionRandomizePatches>().LogInformation("CrusadeEventsTimeline next event delay has been rolled. Delay={Delay}, Identifier={Identifier}", delay, identifier);
                return delay;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ContextActionRandomizePatches>().LogError(ex, "Error while rolling next event delay");
                throw;
            }
        }
    }
}
