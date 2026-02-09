using AutoMapper;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.Utility;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Config.Mapping
{
    public class GameProfile : Profile
    {
        public GameProfile()
        {
            CreateMap<AbilityData, NetworkAbility>().ConstructUsing(Create);

            CreateMap<SpellSlot, NetworkSpellSlot>().ConstructUsing(x => Create(x));

            CreateMap<SpellSlot, NetworkAbilityParamSpellSlot>().ConstructUsing(Create);

            CreateMap<TargetWrapper, NetworkTargetWrapper>().ConstructUsing(x => Create(x))
                .ForMember(x => x.Point, o => o.Ignore());
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
                SpellbookId = abilityData.Spellbook?.Blueprint.Name.Key,
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
