using AutoMapper;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Config.Mapping
{
    public class NetworkingProfile : Profile
    {
        public NetworkingProfile()
        {
            CreateMap<NetworkMultiplayerSettings, NetworkServerConfiguration>()
                .ForMember(x => x.Host, opt => opt.MapFrom(f => f.Host))
                .ForMember(x => x.UseIPv6, opt => opt.MapFrom(f => f.UseIPv6))
                .ForMember(x => x.PortRangeStart, opt => opt.MapFrom(f => f.HostPortRangeStart))
                .ForMember(x => x.PortRangeEnd, opt => opt.MapFrom(f => f.HostPortRangeEnd))
                .ForMember(x => x.AwaiterTimeout, opt => opt.MapFrom(f => f.NetworkAwaiterTimeout))
                ;

            CreateMap<ExternalServer, ExternalServerConfiguration>()
                .ForMember(x => x.Url, opt => opt.MapFrom(f => f.Url))
                .ForMember(x => x.GameHubPath, opt => opt.MapFrom(f => f.GameHubPath))
                ;
        }
    }
}
