using HarmonyLib;
using Kingmaker.Assets.Controllers.GlobalMap;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class RandomEncountersControllerPatches
    {
        [HarmonyPatch(typeof(RandomEncountersController), nameof(RandomEncountersController.RollTravelEncounter))]
        [HarmonyPrefix]
        public static bool RandomEncountersController_RollTravelEncounter_Prefix(ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnGlobalMapBeforeRollTravelEncounter();
            if (!canContinue)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(RandomEncountersController), nameof(RandomEncountersController.RollTravelEncounter))]
        [HarmonyPostfix]
        public static void RandomEncountersController_RollTravelEncounter_Postfix(ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || !__result)
            {
                return;
            }

            var encounter = RandomEncountersController.State.Player.CurrentEncounterData;
            var randomEncounter = new NetworkGlobalMapEncounter
            {
                AvoidanceResult = encounter.AvoidanceCheckResult.ToString(),
                BlueprintId = encounter.Blueprint.AssetGuid.ToString(),
                Position = encounter.Position == null ? null : new NetworkVector3(encounter.Position.Value.x, encounter.Position.Value.y, encounter.Position.Value.z),
                Seed = encounter.RandomCombat.Seed,
                IsTrader = encounter.IsTraderRE,
            };

            Main.Multiplayer.OnGlobalMapEncounterRolled(randomEncounter);
        }
    }
}
