using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.Core.Utils;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class GameStateLookupService : IGameStateLookupService
    {
        private readonly ILogger<GameStateLookupService> _logger;

        public GameStateLookupService(ILogger<GameStateLookupService> logger)
        {
            _logger = logger;
        }

        public List<UnitEntityData> GetActualParty()
        {
            var party = Game.Instance.Player.CapitalPartyMode ? Game.Instance.Player.AllCharacters : Game.Instance.Player.PartyAndPets;
            return party;
        }

        public MapObjectEntityData GetMapObject(string uniqueId)
        {
            return Game.Instance.State.MapObjects.All.FirstOrDefault(o => string.Equals(o.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }

        public UnitEntityData GetUnitEntity(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                return null;
            }

            return Game.Instance.State.Units.All.FirstOrDefault(u => string.Equals(u.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }

        public List<MapObjectEntityData> GetNeareastLootableMapObjects(NetworkVector3 position, float maxDistanceInMeters)
        {
            var targetPoint = position.ToUnityVector3();
            var orderedContainers = Game.Instance.State.MapObjects.All
                .Where(o => o.Interactions.Any(i => i is InteractionLootPart)
                    && GeometryUtils.MechanicsDistance(o.Position, targetPoint) < maxDistanceInMeters)
                .OrderBy(o => (o.Position - targetPoint).magnitude)
                .ToList();

            return orderedContainers;
        }

        public MapObjectEntityData GetNeareastLootBagMapObject(NetworkVector3 position, float maxDistanceInMeters)
        {
            var allNearest = GetNeareastLootableMapObjects(position, maxDistanceInMeters);
            var lootbag = allNearest.FirstOrDefault(o => o is DroppedLoot.EntityData);
            return lootbag;
        }

        public GlobalMapPointView GetGlobalMapPoint(NetworkGlobalMapLocation globalMapLocation)
        {
            if (globalMapLocation == null)
            {
                return null;
            }

            var point = GlobalMapView.Instance?
                .Points
                .FirstOrDefault(p => string.Equals(p.Blueprint.AssetGuid.ToString(), globalMapLocation.Id, StringComparison.OrdinalIgnoreCase));

            return point;
        }

        public GlobalMapArmyPawn GetGlobalMapArmyPawn(NetworkGlobalMapArmy globalMapArmyPawn)
        {
            var armyPawn = GlobalMapView.Instance?.GetArmyView(globalMapArmyPawn.Id);
            return armyPawn;
        }

        public SettlementState GetKingdomSettlement(NetworkKingdomSettlement kingdomSettlement)
        {
            var settlement = KingdomState.Instance?.SettlementsManager.m_SettlementStates.FirstOrDefault(s => string.Equals(s.UniqueId, kingdomSettlement.Id, StringComparison.OrdinalIgnoreCase));
            return settlement;
        }

        public GlobalMapArmyState GetGlobalMapArmy(string id)
        {
            var army = GlobalMapController.State?.Armies.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
            return army;
        }

        public AbilityData FindAbility(UnitEntityData unit, NetworkAbility networkAbility)
        {
            if (networkAbility == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(networkAbility.SpellbookId))
            {
                return FindAbilityInSpellbook(unit, networkAbility);
            }

            if (!string.IsNullOrEmpty(networkAbility.ConvertedFromId))
            {
                var conversionAbility = GetAbility(unit, networkAbility.ConvertedFromId, networkAbility.BlueprintId) ?? GetItemAbility(unit, networkAbility);
                if (conversionAbility == null)
                {
                    _logger.LogError("Unable to find ability for conversion. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, networkAbility.ConvertedFromId);
                    return null;
                }

                var convertedAbility = GetConvertedAbility(conversionAbility, networkAbility);
                if (convertedAbility == null)
                {
                    _logger.LogError("Unable to find ability in conversion list. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, networkAbility.ConvertedFromId);
                }

                return convertedAbility;
            }

            var fromAbilities = GetAbility(unit, networkAbility);
            if (fromAbilities != null)
            {
                _logger.LogInformation("Ability has been found in abilities. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, networkAbility.Id);
                return fromAbilities;
            }

            return null;
        }

        public Spellbook GetSpellbook(UnitEntityData unit, string spellbookId)
        {
            var spellBook = unit.Spellbooks.FirstOrDefault(s => string.Equals(s.Blueprint.AssetGuid.ToString(), spellbookId, StringComparison.OrdinalIgnoreCase));
            return spellBook;
        }

        public AbilityData GetKnownSpell(Spellbook spellbook, string abilityId, string abilityBlueprintId, int spellLevel, int? metamagic)
        {
            if (spellbook.m_KnownSpells.Length <= spellLevel)
            {
                return null;
            }

            var spells = spellbook.m_KnownSpells[spellLevel];
            var spell = GetSpell(spells, abilityId, abilityBlueprintId, metamagic);
            return spell;
        }

        public SpellSlot GetSpellSlot(Spellbook spellbook, NetworkSpellSlot networkSpellSlot, int spellLevel)
        {
            if (spellbook == null || networkSpellSlot == null)
            {
                return null;
            }

            if (spellbook.m_MemorizedSpells.Length <= spellLevel)
            {
                return null;
            }

            var spellSlots = spellbook.m_MemorizedSpells[spellLevel];
            var spellSlot = spellSlots.FirstOrDefault(s => s.Index == networkSpellSlot.Index && s.Type == networkSpellSlot.Type);

            return spellSlot;
        }

        public AbilityData GetKnownSpell(Spellbook spellbook, NetworkAbility ability)
        {
            return GetKnownSpell(spellbook, ability.Id, ability.BlueprintId, ability.SpellLevel, ability.Metamagic);
        }

        public List<AreaEffectEntityData> GetAreaEffects()
        {
            return [.. Game.Instance.State.AreaEffects];
        }

        public AreaEffectEntityData GetAreaEffect(NetworkAreaEffect networkAreaEffect)
        {
            var areaEffect = Game.Instance.State.AreaEffects.FirstOrDefault(a => string.Equals(a.UniqueId, networkAreaEffect.Id, StringComparison.OrdinalIgnoreCase));
            return areaEffect;
        }

        public AiAction FindAIAction(UnitEntityData unit, NetworkAIAction networkAIAction)
        {
            var action = FindAIAction([unit.Brain.GetAvailableAutoUseAbility()?.DefaultAiAction], networkAIAction)
                ?? FindAIAction([.. unit.Brain.CustomActions.Cast<AiAction>()], networkAIAction)
                ?? FindAIAction(unit.Brain.AvailableActions, networkAIAction)
                ?? FindAIAction(unit.Brain.Actions, networkAIAction);

            return action;
        }

        public AbilityData GetSpecialSpell(Spellbook spellbook, NetworkAbility ability)
        {
            return GetSpecialSpell(spellbook, ability.Id, ability.BlueprintId, ability.SpellLevel, ability.Metamagic);
        }

        public AbilityData GetCustomSpell(Spellbook spellbook, NetworkAbility ability)
        {
            return GetCustomSpell(spellbook, ability.Id, ability.BlueprintId, ability.SpellLevel, ability.Metamagic);
        }

        private AbilityData GetItemAbility(UnitEntityData unit, NetworkAbility networkAbility)
        {
            if (networkAbility.SourceItem == null)
            {
                return null;
            }

            var itemAbility = unit.Abilities.Enumerable.FirstOrDefault(a => a.SourceItem?.HoldingSlot != null && string.Equals(a.SourceItem.Blueprint.AssetGuid.ToString(), networkAbility.SourceItem.BlueprintId, StringComparison.OrdinalIgnoreCase));
            var itemAbilityData = itemAbility?.Data;
            if (itemAbilityData != null)
            {
                _logger.LogInformation("Ability has been found in item abilities. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}, ItemName={ItemName}", unit.UniqueId, networkAbility.Id, itemAbilityData.NameForAcronym, networkAbility.SourceItem.Name);
            }

            return itemAbilityData;
        }

        private AiAction FindAIAction(List<AiAction> actions, NetworkAIAction networkAIAction)
        {
            var action = actions?.FirstOrDefault(a => a != null && string.Equals(a.Blueprint.AssetGuid.ToString(), networkAIAction.Id, StringComparison.OrdinalIgnoreCase));
            return action;
        }

        private AbilityData GetMemorizedSpell(Spellbook spellbook, NetworkAbility ability)
        {
            return GetMemorizedSpell(spellbook, ability.Id, ability.BlueprintId, ability.SpellLevel, ability.Metamagic);
        }

        private AbilityData GetCustomSpell(Spellbook spellbook, string abilityId, string abilityBlueprintId, int spellLevel, int? metamagic)
        {
            if (spellbook.m_CustomSpells.Length <= spellLevel)
            {
                return null;
            }

            var spells = spellbook.m_CustomSpells[spellLevel];
            var spell = GetSpell(spells, abilityId, abilityBlueprintId, metamagic);
            return spell;
        }

        private AbilityData GetSpecialSpell(Spellbook spellbook, string abilityId, string abilityBlueprintId, int spellLevel, int? metamagic)
        {
            if (spellbook.m_SpecialSpells.Length <= spellLevel)
            {
                return null;
            }

            var spells = spellbook.m_SpecialSpells[spellLevel];
            var spell = GetSpell(spells, abilityId, abilityBlueprintId, metamagic);
            return spell;
        }

        private AbilityData GetMemorizedSpell(Spellbook spellbook, string abilityId, string abilityBlueprintId, int spellLevel, int? metamagic)
        {
            if (spellbook.m_MemorizedSpells.Length <= spellLevel)
            {
                return null;
            }

            var spells = spellbook.m_MemorizedSpells[spellLevel].Where(x => x.Spell != null).Select(x => x.Spell).ToList();
            var spell = GetSpell(spells, abilityId, abilityBlueprintId, metamagic);
            return spell;
        }

        private AbilityData GetAbility(UnitEntityData unit, NetworkAbility networkAbility)
        {
            return GetAbility(unit, networkAbility.Id, networkAbility.BlueprintId);
        }

        private AbilityData GetAbility(UnitEntityData unit, string abilityId, string abilityBlueprintId)
        {
            var ability = unit.Abilities.Enumerable.FirstOrDefault(a => string.Equals(a.Data.UniqueId, abilityId, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(a.Data.Blueprint.AssetGuid.ToString(), abilityBlueprintId, StringComparison.OrdinalIgnoreCase));

            return ability?.Data;
        }

        private AbilityData GetSpell(List<AbilityData> spells, string abilityId, string abilityBlueprintId, int? metamagic)
        {
            var matchedSpells = spells.Where(s => string.Equals(s.UniqueId, abilityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Blueprint.AssetGuid.ToString(), abilityBlueprintId, StringComparison.OrdinalIgnoreCase));

            if (metamagic.HasValue)
            {
                var meta = (Metamagic)metamagic.Value;
                matchedSpells.Where(x => x.MetamagicData != null && x.MetamagicData.Has(meta));
            }

            var spell = matchedSpells.FirstOrDefault();
            return spell;
        }

        private AbilityData FindAbilityInSpellbook(UnitEntityData unit, NetworkAbility networkAbility)
        {
            var spellbook = GetSpellbook(unit, networkAbility.SpellbookId);
            if (spellbook == null)
            {
                _logger.LogError("Unable to find ability due to missing spellbook. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookId={SpellbookId}", unit.UniqueId, networkAbility.Id, networkAbility.SpellbookId);
                return null;
            }

            if (!string.IsNullOrEmpty(networkAbility.ConvertedFromId))
            {
                var spellConversionSource = GetKnownSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.BlueprintId, networkAbility.SpellLevel, networkAbility.Metamagic)
                    ?? GetMemorizedSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.BlueprintId, networkAbility.SpellLevel, networkAbility.Metamagic)
                    ?? GetCustomSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.BlueprintId, networkAbility.SpellLevel, networkAbility.Metamagic)
                    ?? GetSpecialSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.BlueprintId, networkAbility.SpellLevel, networkAbility.Metamagic);

                if (spellConversionSource == null)
                {
                    _logger.LogError("Can't find spell conversion source for converted ability. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}, ConvertedAbilityId={ConvertedAbilityId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                var convertedAbility = GetConvertedAbility(spellConversionSource, networkAbility);
                if (convertedAbility == null)
                {
                    _logger.LogError("Can't find target ability in spell conversion list. UnitId={UnitId}, AbilityId={abilityId}, SpellbookName={SpellbookName}, ConvertedAbilityId={ConvertedAbilityId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                _logger.LogInformation("Converted spell has been found. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return convertedAbility;
            }

            var knownSpell = GetKnownSpell(spellbook, networkAbility);
            if (knownSpell != null)
            {
                _logger.LogInformation("Spell has been found in known spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return knownSpell;
            }

            var memorizedSpell = GetMemorizedSpell(spellbook, networkAbility);
            if (memorizedSpell != null)
            {
                _logger.LogInformation("Spell has been found in memorized spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return memorizedSpell;
            }

            var customSpell = GetCustomSpell(spellbook, networkAbility);
            if (customSpell != null)
            {
                _logger.LogInformation("Spell has been found in custom spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return customSpell;
            }

            var specialSpell = GetSpecialSpell(spellbook, networkAbility);
            if (specialSpell != null)
            {
                _logger.LogInformation("Spell has been found in special spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return customSpell;
            }

            var fromAbilities = GetAbility(unit, networkAbility);
            if (fromAbilities != null)
            {
                _logger.LogInformation("Spell has been found in abilities. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, networkAbility.Name, spellbook.Blueprint.Name);
                return fromAbilities;
            }

            return null;
        }

        private AbilityData GetConvertedAbility(AbilityData conversionSource, NetworkAbility networkAbility)
        {
            var convertedSpell = conversionSource.GetConversions().FirstOrDefault(
                    c => string.Equals(c.UniqueId, networkAbility.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Blueprint.AssetGuid.ToString(), networkAbility.BlueprintId, StringComparison.OrdinalIgnoreCase));

            return convertedSpell;
        }
    }
}
