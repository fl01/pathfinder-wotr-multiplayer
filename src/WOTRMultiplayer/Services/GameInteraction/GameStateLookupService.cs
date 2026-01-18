using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class GameStateLookupService : IGameStateLookupService
    {
        private readonly ILogger<GameStateLookupService> _logger;

        public GameStateLookupService(ILogger<GameStateLookupService> logger)
        {
            _logger = logger;
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

        public List<MapObjectEntityData> GetNeareastLootableMapObjects(NetworkVector3 position)
        {
            var targetPoint = new Vector3(position.X, position.Y, position.Z);
            var orderedContainers = Game.Instance.State.MapObjects.All
                .Where(o => o.Interactions.Any(i => i is InteractionLootPart))
                .OrderBy(o => (o.Position - targetPoint).magnitude)
                .ToList();

            return orderedContainers;
        }

        public MapObjectEntityData GetNeareastLootBagMapObject(NetworkVector3 position)
        {
            var allNearest = GetNeareastLootableMapObjects(position);
            var lootbag = allNearest.FirstOrDefault(o => o is DroppedLoot.EntityData);
            return lootbag;
        }

        public GlobalMapPointView GetGlobalMapPoint(NetworkGlobalMapLocation globalMapLocation)
        {
            var point = GlobalMapView.Instance?
                .Points
                .FirstOrDefault(p => string.Equals(p.Blueprint.AssetGuid.ToString(), globalMapLocation.Id, StringComparison.OrdinalIgnoreCase));

            return point;
        }

        public GlobalMapArmyPawn GetGlobalMapArmyPawn(NetworkGlobalMapArmyPawn globalMapArmyPawn)
        {
            var armyPawn = GlobalMapView.Instance?.GetArmyView(globalMapArmyPawn.Id);
            return armyPawn;
        }

        public GlobalMapArmyState GetGlobalMapArmy(string id)
        {
            var army = GlobalMapController.State?.Armies.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
            return army;
        }

        public AbilityData GetKnownSpell(Spellbook spellbook, string abilityId, string abilityName)
        {
            for (int level = 0; level < spellbook.m_KnownSpells.Length; level++)
            {
                var spellLevel = spellbook.m_KnownSpells[level];
                var spellSlot = spellLevel.FirstOrDefault(s => string.Equals(s.UniqueId, abilityId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.NameForAcronym, abilityName, StringComparison.OrdinalIgnoreCase));

                if (spellSlot != null)
                {
                    return spellSlot;
                }
            }

            return null;
        }
        public AbilityData FindAbility(NetworkAbility networkAbility)
        {
            var caster = GetUnitEntity(networkAbility.CasterId);
            if (caster == null)
            {
                return null;
            }

            return FindAbility(caster, networkAbility);
        }

        public AbilityData FindAbility(UnitEntityData unit, NetworkAbility networkAbility)
        {
            if (!string.IsNullOrEmpty(networkAbility.SpellbookId))
            {
                return FindAbilityInSpellbook(unit, networkAbility);
            }

            if (!string.IsNullOrEmpty(networkAbility.ConvertedFromId))
            {
                var conversionAbility = unit.Abilities.Enumerable.FirstOrDefault(a => string.Equals(a.Data.UniqueId, networkAbility.ConvertedFromId, StringComparison.OrdinalIgnoreCase));
                if (conversionAbility == null)
                {
                    _logger.LogInformation("Unable to find ability for conversion. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, networkAbility.ConvertedFromId);
                    return null;
                }
                var convertedAbility = GetConvertedAbility(conversionAbility.Data, networkAbility);
                if (convertedAbility == null)
                {
                    _logger.LogInformation("Unable to find ability in conversion list. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, networkAbility.ConvertedFromId);
                }

                return convertedAbility;
            }

            var byAbilityId = unit.Abilities.Enumerable.FirstOrDefault(a => string.Equals(a.Data.UniqueId, networkAbility.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.Data.NameForAcronym, networkAbility.Name, StringComparison.OrdinalIgnoreCase));

            if (byAbilityId != null)
            {
                _logger.LogInformation("Ability has been found by abilityId. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, networkAbility.Id);
                return byAbilityId.Data;
            }

            return null;
        }

        private AbilityData FindAbilityInSpellbook(UnitEntityData unit, NetworkAbility networkAbility)
        {
            var spellbook = unit.Spellbooks.FirstOrDefault(s => string.Equals(s.Blueprint.Name.Key, networkAbility.SpellbookId));
            if (spellbook == null)
            {
                _logger.LogError("Unable to 4find ability due to missing spellbook. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookId={SpellbookId}", unit.UniqueId, networkAbility.Id, networkAbility.SpellbookId);
                return null;
            }

            if (!string.IsNullOrEmpty(networkAbility.ConvertedFromId))
            {
                var spellConversionSource = GetKnownSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.Name) ?? GetMemorizedSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.Name);
                if (spellConversionSource == null)
                {
                    _logger.LogError("Can't find spell conversion source for converted ability. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}, ConvertedAbilityId={ConvertedAbilityId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                var convertedSpell = GetConvertedAbility(spellConversionSource, networkAbility);
                if (convertedSpell == null)
                {
                    _logger.LogError("Can't find target ability in spell conversion list. UnitId={UnitId}, AbilityId={abilityId}, SpellbookName={SpellbookName}, ConvertedAbilityId={ConvertedAbilityId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                _logger.LogInformation("Converted spell has been found. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return convertedSpell;
            }

            var knownSpell = GetKnownSpell(spellbook, networkAbility.Id, networkAbility.Name);
            if (knownSpell != null)
            {
                _logger.LogInformation("Spell has been found in known spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return knownSpell;
            }

            var memorizedSpell = GetMemorizedSpell(spellbook, networkAbility.Id, networkAbility.Name);
            if (memorizedSpell != null)
            {
                _logger.LogInformation("Spell has been found in memorized spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return memorizedSpell;
            }

            return null;
        }

        private AbilityData GetMemorizedSpell(Spellbook spellbook, string abilityId, string abilityName)
        {
            for (int level = 0; level < spellbook.m_MemorizedSpells.Length; level++)
            {
                var spellLevel = spellbook.m_MemorizedSpells[level];
                var spellSlot = spellLevel.FirstOrDefault(s => string.Equals(s.Spell?.UniqueId, abilityId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Spell?.NameForAcronym, abilityName, StringComparison.OrdinalIgnoreCase));

                if (spellSlot != null)
                {
                    return spellSlot.Spell;
                }
            }

            return null;
        }

        private AbilityData GetConvertedAbility(AbilityData conversionSource, NetworkAbility networkAbility)
        {
            var convertedSpell = conversionSource.GetConversions().FirstOrDefault(
                    c => string.Equals(c.UniqueId, networkAbility.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.NameForAcronym, networkAbility.Name, StringComparison.OrdinalIgnoreCase));

            return convertedSpell;
        }
    }
}
