using HarmonyLib;
using Kingmaker.Controllers.MapObjects;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using WOTRMultiplayer.MP.Entities.MapObjects;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class PerceptionPatches
    {
        [HarmonyPatch(typeof(PartyPerceptionController), nameof(PartyPerceptionController.RollPerception))]
        [HarmonyPrefix]
        public static bool PartyPerceptionController_RollPerception_Prefix(UnitEntityData character, StaticEntityData data)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var shouldContinue = Main.Multiplayer.CanMakePerceptionCheck(character.UniqueId, data.UniqueId);
            return shouldContinue;
        }

        [HarmonyPatch(typeof(PartyPerceptionController), nameof(PartyPerceptionController.RollPerception))]
        [HarmonyPostfix]
        public static void PartyPerceptionController_RollPerception_Postfix(UnitEntityData character, StaticEntityData data)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var check = new NetworkPerceptionCheck
            {
                UnitId = character.UniqueId,
                MapObject = new NetworkMapObject
                {
                    Id = data.UniqueId,
                    Position = new MP.Entities.NetworkVector3(data.Position.x, data.Position.y, data.Position.z)
                }
            };

            Main.Multiplayer.OnPerceptionCheck(check);
        }
    }
}
