using AutoMapper;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Mapping
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
        }
    }
}
