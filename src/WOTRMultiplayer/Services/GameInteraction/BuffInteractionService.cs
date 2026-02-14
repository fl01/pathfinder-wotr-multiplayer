using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Parts;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog;
using WOTRMultiplayer.Config.Mapping;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class BuffInteractionService : IBuffInteractionService
    {
        private readonly ILogger<BuffInteractionService> _logger;
        private readonly IPlayerNotificationService _playerNotificationService;
        private readonly IGameStateLookupService _gameStateLookupService;
        private readonly IMapper _mapper;

        private TimeSpan BuffBaseTime => Game.Instance.TimeController.GameTime;

        public BuffInteractionService(
            ILogger<BuffInteractionService> logger,
            IPlayerNotificationService playerNotificationService,
            IGameStateLookupService gameStateLookupService,
            IMapper mapper)
        {
            _logger = logger;
            _playerNotificationService = playerNotificationService;
            _gameStateLookupService = gameStateLookupService;
            _mapper = mapper;
        }

        public NetworkUnitBuffCollection GetUnitBuffs(UnitEntityData unit)
        {
            var syncableBuffs = GetSyncableUnitBuffs(unit);
            var collection = new NetworkUnitBuffCollection
            {
                Buffs = _mapper.Map<List<NetworkBuff>>(syncableBuffs, x => x.Items[GameProfile.BuffBaseTimeItem] = BuffBaseTime),
                NegativeLevels = _mapper.Map<List<NetworkUnitNegativeLevelsData>>(unit.Get<UnitPartNegativeLevels>()?.m_LevelsData ?? [], x => x.Items[GameProfile.BuffBaseTimeItem] = BuffBaseTime)
            };
            return collection;
        }

        public void UpdateUnitBuffs(UnitEntityData unit, NetworkUnitBuffCollection unitBuffCollection)
        {
            var buffBaseTime = BuffBaseTime;
            var localUnitBuffs = GetSyncableUnitBuffs(unit);
            var remoteUnitBuffs = unitBuffCollection.Buffs.ToList();

            for (int i = remoteUnitBuffs.Count - 1; i >= 0; i--)
            {
                var networkBuff = remoteUnitBuffs[i];
                var buff = localUnitBuffs.FirstOrDefault(x => (string.Equals(x.UniqueId, networkBuff.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Blueprint.AssetGuid.ToString(), networkBuff.BlueprintId, StringComparison.OrdinalIgnoreCase)));

                if (buff == null)
                {
                    _logger.LogWarning("Unable to find buff to update. UnitId={UnitId}, Id={Id}, Rank={Rank}, BlueprintId={BlueprintId}, Name={Name}, Duration={Duration}", unit.UniqueId, networkBuff.Id, networkBuff.Rank, networkBuff.BlueprintId, networkBuff.Name, networkBuff.TimeLeft);
                    continue;
                }

                UpdateExistingUnitBuff(unit, buff, buffBaseTime, networkBuff);
                remoteUnitBuffs.Remove(networkBuff);
                localUnitBuffs.Remove(buff);
            }

            var buffsToRemove = localUnitBuffs.Where(x => !x.Hidden).ToList();
            RemoveUnitBuffs(unit, buffsToRemove);

            var buffsToCreate = remoteUnitBuffs.Where(x => !x.IsHidden).ToList();
            CreateUnitBuffs(unit, buffsToCreate, buffBaseTime);

            UpdateNegativeLevels(unit, unitBuffCollection.NegativeLevels);

            unit.Buffs.UpdateNextEvent();
            _logger.LogInformation("Unit buffs have been updated. UnitId={UnitId}", unit.UniqueId);
        }

        private void UpdateNegativeLevels(UnitEntityData unit, List<NetworkUnitNegativeLevelsData> negativeLevels)
        {
            if (negativeLevels.Count == 0)
            {
                unit.Remove<UnitPartNegativeLevels>();
                return;
            }
        }

        private void CreateUnitBuffs(UnitEntityData unit, List<NetworkBuff> remoteUnitBuffs, TimeSpan buffBaseTime)
        {
            if (remoteUnitBuffs.Count == 0)
            {
                return;
            }

            _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.Combat.Buffs.AddedBuffs.Key, CombatTextSeverity.Debug, new UnitEntityLog(unit.UniqueId), string.Join(", ", remoteUnitBuffs.Select(x => x.Name)));
            foreach (var buffToAdd in remoteUnitBuffs)
            {
                var caster = _gameStateLookupService.GetUnitEntity(buffToAdd.CasterId) ?? unit;
                var buff = ResourcesLibrary.TryGetBlueprint<BlueprintBuff>(buffToAdd.BlueprintId);
                if (buff == null)
                {
                    _logger.LogError("Missing blueprint for a buff. BlueprintId={BlueprintId}", buffToAdd.BlueprintId);
                    continue;
                }

                MechanicsContext parent = GetBuffParentContext(unit, buffToAdd);
                var context = new MechanicsContext(caster, unit, buff, parent);

                var duration = buffToAdd.IsPermanent ? TimeSpan.MaxValue : buffToAdd.TimeLeft;
                var newBuff = unit.Buffs.AddBuff(buff, context, duration);
                newBuff.NextTickTime = buffToAdd.NextTickTime == TimeSpan.MaxValue ? TimeSpan.MaxValue : buffBaseTime.SafeAdd(buffToAdd.NextTickTime);
                if (buffToAdd.Rank > 0 && newBuff.Rank != buffToAdd.Rank)
                {
                    newBuff.SetRank(buffToAdd.Rank);
                }

                _logger.LogInformation("Buff has been added. UnitId={UnitId}, Id={Id}, Rank={Rank}, BlueprintId={BlueprintId}, Name={Name}, IsPermanent={IsPermanent}, PlannedDuration={PlannedDuration}", unit.UniqueId, newBuff.UniqueId, newBuff.Rank, buffToAdd.BlueprintId, newBuff.NameForAcronym, newBuff.IsPermanent, newBuff.PlannedDuration);
            }
        }

        private MechanicsContext GetBuffParentContext(UnitEntityData unit, NetworkBuff buffToAdd)
        {
            if (buffToAdd.SourceAbility == null)
            {
                return null;
            }

            var abilityCaster = _gameStateLookupService.GetUnitEntity(buffToAdd.SourceAbilityCasterId);
            if (abilityCaster == null)
            {
                _logger.LogError("Unable to find caster unit for buff context. UnitId={UnitId}", buffToAdd.SourceAbilityCasterId);
                return null;
            }

            var ability = _gameStateLookupService.FindAbility(abilityCaster, buffToAdd.SourceAbility);
            if (ability == null)
            {
                _logger.LogError("Unable to find ability for buff context. UnitId={UnitId}, AbilityBlueprintId={AbilityBlueprintId}, AbilityName={AbilityName}", buffToAdd.SourceAbilityCasterId, buffToAdd.SourceAbility.BlueprintId, buffToAdd.SourceAbility.Name);
                return null;
            }

            var abilityParams = _mapper.Map<AbilityParams>(buffToAdd.SourceAbilityParams);
            var context = new AbilityExecutionContext(ability, abilityParams, new Kingmaker.Utility.TargetWrapper(unit));
            return context;
        }

        private List<Buff> GetSyncableUnitBuffs(UnitEntityData unit)
        {
            // negative levels are handled separately
            var buffs = unit.Buffs.Enumerable.Where(x => x.Blueprint != BlueprintRoot.Instance.SystemMechanics.NegativeLevelsBuff).ToList();
            return buffs;
        }

        private void RemoveUnitBuffs(UnitEntityData unit, List<Buff> unitBuffs)
        {
            if (unitBuffs.Count == 0)
            {
                return;
            }

            _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.Combat.Buffs.RemovedBuffs.Key, CombatTextSeverity.Debug, new UnitEntityLog(unit.UniqueId), string.Join(", ", unitBuffs.Select(x => x.NameForAcronym)));
            foreach (var buffToRemove in unitBuffs)
            {
                GameHelper.RemoveBuff(unit, buffToRemove.Blueprint);
                _logger.LogInformation("Buff has been removed. UnitId={UnitId}, Id={Id}, Name={Name}", unit.UniqueId, buffToRemove.UniqueId, buffToRemove.NameForAcronym);
            }
        }

        private void UpdateExistingUnitBuff(UnitEntityData unit, Buff buff, TimeSpan buffBaseTime, NetworkBuff networkBuff)
        {
            try
            {
                if (!buff.IsPermanent)
                {
                    buff.SetDuration(networkBuff.TimeLeft);
                    if (!buff.Hidden && buff.Rank != networkBuff.Rank)
                    {
                        buff.SetRank(networkBuff.Rank);
                    }
                }

                buff.NextResourceSpendingTime = networkBuff.NextResourceSpendingTime == TimeSpan.MaxValue ? TimeSpan.MaxValue : buffBaseTime.SafeAdd(networkBuff.NextResourceSpendingTime);
                buff.NextTickTime = networkBuff.NextTickTime == TimeSpan.MaxValue ? TimeSpan.MaxValue : buffBaseTime.SafeAdd(networkBuff.NextTickTime);

                _logger.LogDebug("Updated buff. UnitId={UnitId}, Id={Id}, Name={Name}, Duration={Duration}, Rank={Rank}, IsHidden={IsHidden}, NextResourceSpendingTime={NextResourceSpendingTime}, NextTickTime={NextTickTime}, BuffBaseTime={BuffBaseTime} NetworkNextTickTime={NetworkNextTickTime}, NetworkNextPendingTime={NetworkNextPendingTime}",
                    unit.UniqueId, buff.UniqueId, buff.NameForAcronym, networkBuff.TimeLeft, networkBuff.Rank, networkBuff.IsHidden, buff.NextResourceSpendingTime, buff.NextTickTime, buffBaseTime, networkBuff.NextTickTime, networkBuff.NextResourceSpendingTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating buff. UnitId={UnitId}, Id={Id}, Name={Name}, Duration={Duration}, NextResourceSpendingTime={NextResourceSpendingTime}, NextTickTime={NextTickTime}",
                    unit.UniqueId, buff.UniqueId, buff.NameForAcronym, networkBuff.TimeLeft, buff.NextResourceSpendingTime, buff.NextTickTime);
            }
        }
    }
}
