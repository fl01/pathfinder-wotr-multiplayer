using System.Collections.Generic;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameStateLookupService
    {
        UnitEntityData GetUnitEntity(string uniqueId);

        MapObjectEntityData GetMapObject(string uniqueId);

        List<MapObjectEntityData> GetNeareastLootableMapObjects(NetworkVector3 position);

        MapObjectEntityData GetNeareastLootBagMapObject(NetworkVector3 position);

        GlobalMapPointView GetGlobalMapPoint(NetworkGlobalMapLocation globalMapLocation);

        GlobalMapArmyPawn GetGlobalMapArmyPawn(NetworkGlobalMapArmyPawn globalMapArmyPawn);

        GlobalMapArmyState GetGlobalMapArmy(string id);

        AbilityData GetKnownSpell(Spellbook spellbook, string abilityId, string abilityName);

        AbilityData FindAbility(UnitEntityData unit, NetworkAbility abilityUse);

        AbilityData FindAbility(NetworkAbility abilityUse);
    }
}
