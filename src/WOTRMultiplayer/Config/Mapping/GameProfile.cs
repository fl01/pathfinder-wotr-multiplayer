using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.Kingdom.UI;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.AreaEffects;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;
using WOTRMultiplayer.Entities.MapObjects;
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

            CreateMap<SettlementState, NetworkKingdomSettlement>().ConstructUsing(x => Create(x))
                .ForAllMembers(x => x.Ignore());

            CreateMap<KingdomEventUIView, NetworkKingdomEvent>().ConstructUsing(x => Create(x))
                .ForAllMembers(x => x.Ignore());

            CreateMap<SettlementBuilding, NetworkKingdomSettlementBuilding>().ConstructUsing(Create)
                .ForAllMembers(x => x.Ignore());

            CreateMap<SettlementGridTopology.Slot, NetworkKingdomSettlementSlot>().ConstructUsing(x => Create(x))
                .ForAllMembers(x => x.Ignore());

            CreateMap<ItemEntity, NetworkItem>().ConstructUsing(x => Create(x))
                .ForAllMembers(x => x.Ignore());

            CreateMap<EntityDataBase, NetworkMapObject>().ConstructUsing(x => Create(x))
                .ForAllMembers(x => x.Ignore());
        }

        private NetworkMapObject Create(EntityDataBase entityDataBase)
        {
            if (entityDataBase == null)
            {
                return null;
            }

            var mapObject = new NetworkMapObject
            {
                Id = entityDataBase.UniqueId,
                Position = entityDataBase.Position.ToNetworkVector3()
            };

            return mapObject;
        }

        private NetworkItem Create(ItemEntity itemEntity)
        {
            if (itemEntity == null)
            {
                return null;
            }

            var item = new NetworkItem
            {
                UniqueId = itemEntity.UniqueId,
                BlueprintId = itemEntity.Blueprint.AssetGuid.ToString(),
                Name = itemEntity.NameForAcronym,
                Count = itemEntity.Count,
                Cost = itemEntity.Cost,
                EnchantmentValue = itemEntity.EnchantmentValue,
                Enchantments = [.. itemEntity.Enchantments.Select(x => x.Blueprint.name)],
                HoldingSlotOwnerId = itemEntity.HoldingSlot?.Owner?.Unit.UniqueId,
                CollectionOwnerRef = itemEntity.Collection?.OwnerRef.Id
            };

            return item;
        }

        private NetworkKingdomSettlementSlot Create(SettlementGridTopology.Slot slot)
        {
            if (slot == null)
            {
                return null;
            }

            var kingdomSettlementSlot = new NetworkKingdomSettlementSlot
            {
                Id = slot.SlotId,
                X = slot.X,
                Y = slot.Y
            };

            return kingdomSettlementSlot;
        }

        private NetworkKingdomSettlementBuilding Create(SettlementBuilding settlementBuilding, ResolutionContext context)
        {
            if (settlementBuilding == null)
            {
                return null;
            }

            var kingdomSettlementBuilding = new NetworkKingdomSettlementBuilding
            {
                Id = settlementBuilding.UniqueId,
                BlueprintId = settlementBuilding.Blueprint.AssetGuid.ToString(),
                Slots = context.Mapper.Map<List<NetworkKingdomSettlementSlot>>(settlementBuilding.Slots)
            };

            return kingdomSettlementBuilding;
        }

        private NetworkKingdomEvent Create(KingdomEventUIView kingdomEventUIView)
        {
            if (kingdomEventUIView?.Event == null)
            {
                return null;
            }

            var kingdomEvent = new NetworkKingdomEvent
            {
                Id = kingdomEventUIView.Event.EventBlueprint.AssetGuid.ToString()
            };

            return kingdomEvent;
        }

        private NetworkKingdomSettlement Create(SettlementState settlementState)
        {
            if (settlementState == null)
            {
                return null;
            }

            var kingdomSettlement = new NetworkKingdomSettlement
            {
                Id = settlementState.UniqueId,
                Name = settlementState.Name,
            };

            return kingdomSettlement;
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
                UnitsInside = [.. areaEffectEntityData.m_UnitsInside.Select(x => x.Reference.UniqueId)],
                Type = NetworkAreaEffectType.Common
            };

            if (areaEffectEntityData.Blueprint.Components?.Any(c => c is AreaEffectPit) ?? false)
            {
                areaEffect.Type = NetworkAreaEffectType.Pit;
            }

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

            var rods = abilityData.CasterUnitPartSpecialMetamagic?.m_MetamagicRodMechanics ?? [];
            var abilityRange = abilityData.OverrideRange ?? abilityData.Range;
            // so far only Reach metamagic (Lesser Reach Wand) requires special case in case of healing spells (cure wounds)
            var sourceAbility = rods.Count > 0 && rods.Any(r => r.rodMechanics.Metamagic == Metamagic.Reach)
                && (abilityRange is AbilityRange.Touch or AbilityRange.Close or AbilityRange.Medium)
                && abilityData.ConvertedFrom != null
                && abilityData.ConvertedFrom.GetConversions().Count() == 0
                    ? abilityData.ConvertedFrom
                    : abilityData;

            var ability = new NetworkAbility
            {
                Id = sourceAbility.UniqueId,
                Name = sourceAbility.NameForAcronym,
                BlueprintId = sourceAbility.Blueprint.AssetGuid.ToString(),
                SpellbookId = sourceAbility.Spellbook?.Blueprint.AssetGuid.ToString(),
                ConvertedFromId = sourceAbility.ConvertedFrom?.UniqueId,
                SpellLevel = sourceAbility.SpellLevel,
                Metamagic = (int?)sourceAbility.MetamagicData?.MetamagicMask,
                ParamSpellLevel = sourceAbility.ParamSpellLevel,
                ParamSpellBookId = sourceAbility.ParamSpellbook?.Blueprint.AssetGuid.ToString(),
                ParamSpellSlot = context.Mapper.Map<NetworkAbilityParamSpellSlot>(sourceAbility.ParamSpellSlot)
            };

            return ability;
        }
    }
}
