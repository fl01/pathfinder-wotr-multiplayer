using AutoMapper;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Ping;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Entities.Vendor;

namespace WOTRMultiplayer.Config.Mapping
{
    public class NetworkMessagesProfile : Profile
    {
        public NetworkMessagesProfile()
        {
            CreateMap<NetworkVector3, Networking.Messages.Contracts.NetworkVector3>()
                .ReverseMap();

            CreateMap<NetworkVector2Int, Networking.Messages.Contracts.NetworkVector2Int>()
                .ReverseMap();

            CreateMap<NetworkTargetWrapper, Networking.Messages.Contracts.NetworkTargetWrapper>()
                .ReverseMap();

            CreateMap<NetworkPlayer, Networking.Messages.Contracts.NetworkPlayer>()
                .ReverseMap();

            CreateMap<NetworkCharacterMove, Networking.Messages.Contracts.NetworkCharacterMove>()
                .ReverseMap();

            CreateMap<NetworkDialogAnswerSuggestion, Networking.Messages.Contracts.NetworkDialogAnswerSuggestion>()
                .ReverseMap();

            CreateMap<NetworkCharacter, Networking.Messages.Contracts.NetworkCharacter>()
                .ForMember(x => x.OwnerId, o => o.MapFrom(f => f.Owner.Id))
                .ReverseMap();

            CreateMap<NetworkClick, Networking.Messages.Contracts.NetworkClick>()
                .ReverseMap();

            CreateMap<NetworkAbility, Networking.Messages.Contracts.NetworkAbility>()
                .ReverseMap();

            CreateMap<NetworkDamageRollValue, Networking.Messages.Contracts.NetworkDamageRollValue>()
                .ReverseMap();

            CreateMap<NetworkUnit, Networking.Messages.Contracts.NetworkUnit>()
                .ReverseMap();

            CreateMap<NetworkActivatableAbility, Networking.Messages.Contracts.NetworkActivatableAbility>()
                .ReverseMap();

            CreateMap<NetworkDamageListRollValue, Networking.Messages.Contracts.NetworkRollValue>()
                .ForMember(m => m.DamageValues, o => o.MapFrom(v => v.Value))
                .ReverseMap()
                .ForMember(m => m.Value, o => o.MapFrom(v => v.DamageValues));

            CreateMap<NetworkNamedIntRollValue, Networking.Messages.Contracts.NetworkRollValue>()
                .ForMember(m => m.NamedIntValues, o => o.MapFrom(v => v.Value))
                .ReverseMap()
                .ForMember(m => m.Value, o => o.MapFrom(v => v.NamedIntValues));

            CreateMap<NetworkIntRollValue, Networking.Messages.Contracts.NetworkRollValue>()
                .ForMember(m => m.Result, o => o.MapFrom(v => v.Value))
                .ReverseMap()
                .ForMember(m => m.Value, o => o.MapFrom(v => v.Result));

            CreateMap<NetworkItem, Networking.Messages.Contracts.NetworkItem>()
                .ReverseMap();

            CreateMap<NetworkDropItem, Networking.Messages.Contracts.NetworkDropItem>()
                .ReverseMap();

            CreateMap<NetworkUseInventoryItem, Networking.Messages.Contracts.NetworkUseInventoryItem>()
                .ReverseMap();

            CreateMap<NetworkEquipmentSlot, Networking.Messages.Contracts.NetworkEquipmentSlot>()
                .ReverseMap();

            CreateMap<NetworkEquipmentSlotPosition, Networking.Messages.Contracts.NetworkEquipmentSlotPosition>()
                .ReverseMap();

            CreateMap<NetworkActiveHandEquipmentSet, Networking.Messages.Contracts.NetworkActiveHandEquipmentSet>()
                .ReverseMap();

            CreateMap<NetworkOvertip, Networking.Messages.Contracts.NetworkOvertip>()
                .ReverseMap();

            CreateMap<NetworkMapObject, Networking.Messages.Contracts.NetworkMapObject>()
                .ReverseMap();

            CreateMap<NetworkPerceptionCheck, Networking.Messages.Contracts.NetworkPerceptionCheck>()
                .ReverseMap();

            CreateMap<NetworkInspectionKnowledgeCheck, Networking.Messages.Contracts.NetworkInspectionKnowledgeCheck>()
                .ReverseMap();

            CreateMap<NetworkStealthPerceptionCheck, Networking.Messages.Contracts.NetworkStealthPerceptionCheck>()
                .ReverseMap();

            CreateMap<NetworkGameSettings, Networking.Messages.Contracts.NetworkGameSettings>()
                .ReverseMap();

            CreateMap<NetworkTurnBasedSettngs, Networking.Messages.Contracts.NetworkTurnBasedSettngs>()
                .ReverseMap();

            CreateMap<NetworkGameMainSettings, Networking.Messages.Contracts.NetworkGameMainSettings>()
                .ReverseMap();

            CreateMap<NetworkAutopauseSettings, Networking.Messages.Contracts.NetworkAutopauseSettings>()
                .ReverseMap();

            CreateMap<NetworkTutorialSettings, Networking.Messages.Contracts.NetworkTutorialSettings>()
                .ReverseMap();

            CreateMap<NetworkMultiplayerSettings, Networking.Messages.Contracts.NetworkMultiplayerSettings>()
                .ReverseMap();

            CreateMap<NetworkCampingRole, Networking.Messages.Contracts.NetworkCampingRole>()
                .ReverseMap();

            CreateMap<NetworkCampingState, Networking.Messages.Contracts.NetworkCampingState>()
                .ReverseMap();

            CreateMap<NetworkRandomEncounter, Networking.Messages.Contracts.NetworkRandomEncounter>()
                .ReverseMap();

            CreateMap<NetworkRestBanter, Networking.Messages.Contracts.NetworkRestBanter>()
                .ReverseMap();

            CreateMap<NetworkAIAction, Networking.Messages.Contracts.NetworkAIAction>()
                .ReverseMap();

            CreateMap<NetworkVendorItemTransfer, Networking.Messages.Contracts.NetworkVendorItemTransfer>()
                .ReverseMap();

            CreateMap<NetworkSpellSlot, Networking.Messages.Contracts.NetworkSpellSlot>()
                .ReverseMap();

            CreateMap<NetworkLevelingClass, Networking.Messages.Contracts.NetworkLevelingClass>()
                .ReverseMap();

            CreateMap<NetworkLevelingArchetype, Networking.Messages.Contracts.NetworkLevelingArchetype>()
                .ReverseMap();

            CreateMap<NetworkLevelingPhase, Networking.Messages.Contracts.NetworkLevelingPhase>()
                .ReverseMap();

            CreateMap<NetworkLevelingSkillPoint, Networking.Messages.Contracts.NetworkLevelingSkillPoint>()
                .ReverseMap();

            CreateMap<NetworkLevelingFeature, Networking.Messages.Contracts.NetworkLevelingFeature>()
                .ReverseMap();

            CreateMap<NetworkLevelingSpell, Networking.Messages.Contracts.NetworkLevelingSpell>()
                .ReverseMap();

            CreateMap<NetworkLevelingAbilityScore, Networking.Messages.Contracts.NetworkLevelingAbilityScore>()
                .ReverseMap();

            CreateMap<NetworkLevelingPortrait, Networking.Messages.Contracts.NetworkLevelingPortrait>()
                .ReverseMap();

            CreateMap<NetworkLevelingVoice, Networking.Messages.Contracts.NetworkLevelingVoice>()
                .ReverseMap();

            CreateMap<NetworkLevelingWarpaint, Networking.Messages.Contracts.NetworkLevelingWarpaint>()
                .ReverseMap();

            CreateMap<NetworkLevelingTattoo, Networking.Messages.Contracts.NetworkLevelingTattoo>()
                .ReverseMap();

            CreateMap<NetworkActionBarSlot, Networking.Messages.Contracts.NetworkActionBarSlot>()
                .ReverseMap();

            CreateMap<NetworkLockpickInteraction, Networking.Messages.Contracts.NetworkLockpickInteraction>()
                .ReverseMap();

            CreateMap<NetworkCombatState, Networking.Messages.Contracts.NetworkCombatState>()
                .ReverseMap();

            CreateMap<NetworkUnitAttack, Networking.Messages.Contracts.NetworkUnitAttack>()
                .ReverseMap();

            CreateMap<NetworkUnitTurnBasedInfo, Networking.Messages.Contracts.NetworkUnitTurnBasedInfo>()
                .ReverseMap();

            CreateMap<NetworkUnitCombatState, Networking.Messages.Contracts.NetworkUnitCombatState>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapLocation, Networking.Messages.Contracts.NetworkGlobalMapLocation>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapTraveler, Networking.Messages.Contracts.NetworkGlobalMapTraveler>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapPosition, Networking.Messages.Contracts.NetworkGlobalMapPosition>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapEncounter, Networking.Messages.Contracts.NetworkGlobalMapEncounter>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapTravel, Networking.Messages.Contracts.NetworkGlobalMapTravel>()
                .ReverseMap();

            CreateMap<NetworkItemsTransfer, Networking.Messages.Contracts.NetworkItemsTransfer>()
                .ReverseMap();

            CreateMap<NetworkLootableEntity, Networking.Messages.Contracts.NetworkLootableEntity>()
                .ReverseMap();

            CreateMap<NetworkContentState, Networking.Messages.Contracts.NetworkContentState>()
                .ReverseMap();

            CreateMap<NetworkDLC, Networking.Messages.Contracts.NetworkDLC>()
                .ReverseMap();

            CreateMap<NetworkMod, Networking.Messages.Contracts.NetworkMod>()
                .ReverseMap();

            CreateMap<NetworkDiscrepantMod, Networking.Messages.Contracts.NetworkDiscrepantMod>()
                .ReverseMap();

            CreateMap<NetworkDiscrepantDLC, Networking.Messages.Contracts.NetworkDiscrepantDLC>()
                .ReverseMap();

            CreateMap<NetworkDialogPopup, Networking.Messages.Contracts.NetworkDialogPopup>()
                .ReverseMap();

            CreateMap<NetworkNewGameSequencePhase, Networking.Messages.Contracts.NetworkNewGameSequencePhase>()
                .ReverseMap();

            CreateMap<NetworkPolymorphicItem, Networking.Messages.Contracts.NetworkPolymorphicItem>()
                .ReverseMap();

            CreateMap<NetworkEquipmentSwapContext, Networking.Messages.Contracts.NetworkEquipmentSwapContext>()
                .ReverseMap();

            CreateMap<NetworkPing, Networking.Messages.Contracts.NetworkPing>()
                .ReverseMap();

            CreateMap<NetworkAreaTransition, Networking.Messages.Contracts.NetworkAreaTransition>()
                .ReverseMap();

            CreateMap<NetworkArea, Networking.Messages.Contracts.NetworkArea>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapArmy, Networking.Messages.Contracts.NetworkGlobalMapArmy>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapArmySquadSlot, Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapKingdomSettlement, Networking.Messages.Contracts.NetworkGlobalMapKingdomSettlement>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapCommonPopup, Networking.Messages.Contracts.NetworkGlobalMapCommonPopup>()
                .ReverseMap();

            CreateMap<NetworkTacticalUnitUseAbilityCommand, Networking.Messages.Contracts.NetworkTacticalUnitUseAbilityCommand>()
                .ReverseMap();

            CreateMap<NetworkTacticalUnitAttackCommand, Networking.Messages.Contracts.NetworkTacticalUnitAttackCommand>()
                .ReverseMap();

            CreateMap<NetworkTacticalUnitMoveToCommand, Networking.Messages.Contracts.NetworkTacticalUnitMoveToCommand>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapArmyLeader, Networking.Messages.Contracts.NetworkGlobalMapArmyLeader>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapResourceOrder, Networking.Messages.Contracts.NetworkGlobalMapResourceOrder>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapUnitRecruitmentOrder, Networking.Messages.Contracts.NetworkGlobalMapUnitRecruitmentOrder>()
                .ReverseMap();

            CreateMap<NetworkGlobalMapMagicSpell, Networking.Messages.Contracts.NetworkGlobalMapMagicSpell>()
                .ReverseMap();

            CreateMap<NetworkUnitDescriptor, Networking.Messages.Contracts.NetworkUnitDescriptor>()
                .ReverseMap();

            CreateMap<NetworkUnitState, Networking.Messages.Contracts.NetworkUnitState>()
                .ReverseMap();

            CreateMap<NetworkTrapDisarm, Networking.Messages.Contracts.NetworkTrapDisarm>()
                .ReverseMap();
        }
    }
}
