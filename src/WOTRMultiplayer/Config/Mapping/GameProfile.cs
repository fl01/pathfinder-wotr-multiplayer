using System;
using AutoMapper;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.Utility;
using WOTRMultiplayer.Entities;
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
                AbilityParams = context.Mapper.Map<NetworkAbilityParams>(buff.Context.Params)
            };

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
