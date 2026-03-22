using System.Collections.Generic;
using Kingmaker.AI;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;
using WOTRMultiplayer.Entities.Spells;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameStateLookupService
    {
        UnitEntityData GetUnitEntity(string uniqueId);

        MapObjectEntityData GetMapObject(string uniqueId);

        List<MapObjectEntityData> GetNeareastLootableMapObjects(NetworkVector3 position, float maxDistanceInMeters);

        MapObjectEntityData GetNeareastLootBagMapObject(NetworkVector3 position, float maxDistanceInMeters);

        GlobalMapPointView GetGlobalMapPoint(NetworkGlobalMapLocation globalMapLocation);

        GlobalMapArmyPawn GetGlobalMapArmyPawn(NetworkGlobalMapArmy globalMapArmyPawn);

        GlobalMapArmyState GetGlobalMapArmy(string id);

        AbilityData FindAbility(UnitEntityData unit, NetworkAbility ability);

        Spellbook GetSpellbook(UnitEntityData unit, string spellbookId);

        AbilityData GetKnownSpell(Spellbook spellbook, string abilityId, string abilityBlueprintId, int spellLevel, int? metamagic);

        AbilityData GetKnownSpell(Spellbook spellbook, NetworkAbility ability);

        AbilityData GetCustomSpell(Spellbook spellbook, NetworkAbility ability);

        SpellSlot GetSpellSlot(Spellbook spellbook, NetworkSpellSlot networkSpellSlot, int spellLevel);

        List<AreaEffectEntityData> GetAreaEffects();

        AreaEffectEntityData GetAreaEffect(NetworkAreaEffect networkAreaEffect);

        List<UnitEntityData> GetActualParty();

        AiAction FindAIAction(UnitEntityData unit, NetworkAIAction networkAIAction);

        SettlementState GetKingdomSettlement(NetworkKingdomSettlement settlement);

        AbilityData GetSpecialSpell(Spellbook spellbook, NetworkAbility networkAbility);
    }
}
