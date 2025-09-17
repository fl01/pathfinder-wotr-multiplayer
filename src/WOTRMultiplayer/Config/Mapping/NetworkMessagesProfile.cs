using AutoMapper;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.MP.Entities.Settings;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Units;
using WOTRMultiplayer.MP.Entities.Vendor;

namespace WOTRMultiplayer.Config.Mapping
{
    public class NetworkMessagesProfile : Profile
    {
        public NetworkMessagesProfile()
        {
            CreateMap<NetworkVector3, Networking.Messages.Contracts.NetworkVector3>()
                .ReverseMap();

            CreateMap<NetworkPlayer, Networking.Messages.Contracts.NetworkPlayer>()
                .ReverseMap();

            CreateMap<NetworkCharacterMove, Networking.Messages.Contracts.NetworkCharacterMove>()
                .ReverseMap();

            CreateMap<NetworkDialogAnswerSuggestion, Networking.Messages.Contracts.NetworkDialogAnswerSuggestion>()
                .ReverseMap();

            CreateMap<NetworkCharacterOwnership, Networking.Messages.Contracts.NetworkCharacterOwnership>()
                .ReverseMap();

            CreateMap<NetworkClick, Networking.Messages.Contracts.NetworkClick>()
                .ReverseMap();

            CreateMap<NetworkAbility, Networking.Messages.Contracts.NetworkAbility>()
                .ReverseMap();

            CreateMap<NetworkActionsState, Networking.Messages.Contracts.NetworkActionsState>()
                .ReverseMap();

            CreateMap<NetworkCombatAction, Networking.Messages.Contracts.NetworkCombatAction>()
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

            CreateMap<NetworkLootContainer, Networking.Messages.Contracts.NetworkLootContainer>()
                .ReverseMap();

            CreateMap<NetworkItem, Networking.Messages.Contracts.NetworkItem>()
                .ReverseMap();

            CreateMap<NetworkDropItem, Networking.Messages.Contracts.NetworkDropItem>()
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

            CreateMap<NetworkGameSettings, Networking.Messages.Contracts.NetworkGameSettings>()
                .ReverseMap();

            CreateMap<NetworkTurnBasedSettngs, Networking.Messages.Contracts.NetworkTurnBasedSettngs>()
                .ReverseMap();

            CreateMap<NetworkGameMainSettings, Networking.Messages.Contracts.NetworkGameMainSettings>()
                .ReverseMap();

            CreateMap<NetworkAutopauseSettings, Networking.Messages.Contracts.NetworkAutopauseSettings>()
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
        }
    }
}
