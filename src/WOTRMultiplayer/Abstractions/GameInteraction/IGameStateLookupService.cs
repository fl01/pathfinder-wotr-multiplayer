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

        GlobalMapArmyPawn GetGlobalMapArmyPawn(NetworkGlobalMapArmy globalMapArmyPawn);

        GlobalMapArmyState GetGlobalMapArmy(string id);

        AbilityData FindAbility(UnitEntityData unit, NetworkAbility ability);

        Spellbook GetSpellbook(UnitEntityData unit, string spellbookId);

        AbilityData GetKnownSpell(Spellbook spellbook, string abilityId, string abilityBlueprintId, int spellLevel, int? metamagic);

        AbilityData GetKnownSpell(Spellbook spellbook, NetworkAbility ability);

        SpellSlot GetSpellSlot(Spellbook spellbook, int index, SpellSlotType slotType, int spellLevel);
    }
}
