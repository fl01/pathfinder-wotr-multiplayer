using System;
using System.Linq;
using AutoMapper;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Config.Mapping
{
    public class GameProfile : Profile
    {
        public const string BuffBaseTimeItem = "BaseTime";

        public GameProfile()
        {
            CreateMap<AbilityData, NetworkAbility>().ConstructUsing(Create)
                .ForAllMembers(x => x.Ignore());

            CreateMap<SpellSlot, NetworkSpellSlot>().ConstructUsing(x => Create(x))
                .ForAllMembers(x => x.Ignore());

            CreateMap<SpellSlot, NetworkAbilityParamSpellSlot>().ConstructUsing(Create)
                .ForAllMembers(x => x.Ignore());

            CreateMap<TargetWrapper, NetworkTargetWrapper>().ConstructUsing(x => Create(x))
                .ForAllMembers(x => x.Ignore());

            CreateMap<AbilityParams, NetworkAbilityParams>()
                .ReverseMap();

            CreateMap<Buff, NetworkBuff>().ConstructUsing(Create)
                .ForAllMembers(x => x.Ignore());

            CreateMap<UnitPartNegativeLevels.Data, NetworkUnitNegativeLevelsData>().ConstructUsing(Create)
                .ForAllMembers(x => x.Ignore());

            CreateMap<AreaEffectEntityData, NetworkAreaEffect>().ConstructUsing(x => Create(x))
                .ForAllMembers(x => x.Ignore());
        }

        private NetworkAreaEffect Create(AreaEffectEntityData areaEffectEntityData)
        {
            if (areaEffectEntityData == null)
            {
                return null;
            }

            var areaEffect = new NetworkAreaEffect
            {
                Id = areaEffectEntityData.UniqueId,
                Name = areaEffectEntityData.Blueprint.name,
                Position = areaEffectEntityData.m_Position.ToNetworkVector3(),
                UnitsInside = [.. areaEffectEntityData.m_UnitsInside.Select(x => x.Reference.UniqueId)]
            };

            return areaEffect;
        }

        private NetworkUnitNegativeLevelsData Create(UnitPartNegativeLevels.Data negativeLevels, ResolutionContext context)
        {
            if (negativeLevels == null)
            {
                return null;
            }

            var buffBaseTime = (TimeSpan)context.Items[BuffBaseTimeItem];
            var part = new NetworkUnitNegativeLevelsData
            {
                Count = negativeLevels.Count,
                Duration = negativeLevels.EndTime.HasValue ? buffBaseTime - negativeLevels.EndTime.Value : null,
                EnergyDrainType = negativeLevels.Type,
                SavingThrowType = negativeLevels.SavingThrowType
            };

            return part;
        }

        private NetworkBuff Create(Buff buff, ResolutionContext context)
        {
            if (buff == null)
            {
                return null;
            }

            var buffBaseTime = (TimeSpan)context.Items[BuffBaseTimeItem];

            var networkBuff = new NetworkBuff
            {
                Id = buff.UniqueId,
                BlueprintId = buff.Blueprint.AssetGuid.ToString(),
                Name = buff.NameForAcronym,
                IsPermanent = buff.IsPermanent,
                TimeLeft = buff.TimeLeft,
                NextResourceSpendingTime = buff.NextResourceSpendingTime == TimeSpan.MaxValue ? TimeSpan.MaxValue : buff.NextResourceSpendingTime - buffBaseTime,
                NextTickTime = buff.NextTickTime == TimeSpan.MaxValue ? TimeSpan.MaxValue : buff.NextTickTime - buffBaseTime,
                CasterId = buff.Context?.MaybeCaster?.UniqueId,
                Rank = buff.Rank,
                IsHidden = buff.Hidden
            };

            if (buff.Context.ParentContext is AbilityExecutionContext abilityContext)
            {
                networkBuff.SourceAbility = context.Mapper.Map<NetworkAbility>(abilityContext.Ability);
                networkBuff.SourceAbilityParams = context.Mapper.Map<NetworkAbilityParams>(abilityContext.m_Params);
                networkBuff.SourceAbilityCasterId = abilityContext.Ability?.Caster?.Unit.UniqueId;
            }

            return networkBuff;
        }

        private NetworkSpellSlot Create(SpellSlot spellSlot)
        {
            if (spellSlot == null)
            {
                return null;
            }

            var slot = new NetworkSpellSlot
            {
                Index = spellSlot.Index,
                Type = spellSlot.Type
            };

            return slot;
        }

        private NetworkAbilityParamSpellSlot Create(SpellSlot spellSlot, ResolutionContext context)
        {
            if (spellSlot == null)
            {
                return null;
            }

            var param = new NetworkAbilityParamSpellSlot
            {
                Slot = context.Mapper.Map<NetworkSpellSlot>(spellSlot),
                SpellbookId = spellSlot.SpellShell?.Spellbook.Blueprint.AssetGuid.ToString(),
                SpellLevel = spellSlot.SpellLevel
            };

            return param;
        }

        private NetworkTargetWrapper Create(TargetWrapper targetWrapper)
        {
            if (targetWrapper == null)
            {
                return null;
            }

            var wrapper = new NetworkTargetWrapper
            {
                Point = targetWrapper.Point.ToNetworkVector3(),
                Orientation = targetWrapper.Orientation,
                UnitId = targetWrapper.Unit?.UniqueId
            };

            return wrapper;
        }

        private NetworkAbility Create(AbilityData abilityData, ResolutionContext context)
        {
            if (abilityData == null)
            {
                return null;
            }

            var ability = new NetworkAbility
            {
                Id = abilityData.UniqueId,
                Name = abilityData.NameForAcronym,
                BlueprintId = abilityData.Blueprint.AssetGuid.ToString(),
                SpellbookId = abilityData.Spellbook?.Blueprint.AssetGuid.ToString(),
                ConvertedFromId = abilityData.ConvertedFrom?.UniqueId,
                SpellLevel = abilityData.SpellLevel,
                Metamagic = (int?)abilityData.MetamagicData?.MetamagicMask,
                ParamSpellLevel = abilityData.ParamSpellLevel,
                ParamSpellBookId = abilityData.ParamSpellbook?.Blueprint.AssetGuid.ToString(),
                ParamSpellSlot = context.Mapper.Map<NetworkAbilityParamSpellSlot>(abilityData.ParamSpellSlot)
            };

            return ability;
        }
    }
}
