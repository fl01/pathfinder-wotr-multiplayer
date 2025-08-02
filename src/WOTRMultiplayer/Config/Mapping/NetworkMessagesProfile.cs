using AutoMapper;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;

namespace WOTRMultiplayer.Config.Mapping
{
    public class NetworkMessagesProfile : Profile
    {
        public NetworkMessagesProfile()
        {
            CreateMap<NetworkVector3, Networking.Messages.NetworkVector3>()
                .ReverseMap();

            CreateMap<NetworkClick, Networking.Messages.NetworkClick>()
                .ReverseMap();

            CreateMap<NetworkAbility, Networking.Messages.NetworkAbility>()
                .ReverseMap();

            CreateMap<NetworkActionsState, Networking.Messages.NetworkActionsState>()
                .ReverseMap();

            CreateMap<NetworkCombatAction, Networking.Messages.NetworkCombatAction>()
                .ReverseMap();

            CreateMap<NetworkDamageRollValue, Networking.Messages.NetworkDamageRollValue>()
                .ReverseMap();

            CreateMap<NetworkUnit, Networking.Messages.NetworkUnit>()
                .ReverseMap();

            CreateMap<NetworkActivatableAbility, Networking.Messages.NetworkActivatableAbility>()
                .ReverseMap();

            CreateMap<NetworkDamageListRollValue, Networking.Messages.NetworkRollValue>()
                .ForMember(m => m.DamageValues, o => o.MapFrom(v => v.Value))
                .ReverseMap()
                .ForMember(m => m.Value, o => o.MapFrom(v => v.DamageValues));

            CreateMap<NetworkIntRollValue, Networking.Messages.NetworkRollValue>()
                .ForMember(m => m.Result, o => o.MapFrom(v => v.Value))
                .ReverseMap()
                .ForMember(m => m.Value, o => o.MapFrom(v => v.Result));

            CreateMap<NetworkLootContainer, Networking.Messages.NetworkLootContainer>()
                .ReverseMap();

            CreateMap<NetworkLootItem, Networking.Messages.NetworkLootItem>()
                .ReverseMap();
        }
    }
}
