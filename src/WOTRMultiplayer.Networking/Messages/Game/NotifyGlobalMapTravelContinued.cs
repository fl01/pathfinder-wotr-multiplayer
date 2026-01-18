using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapTravelContinued)]
    public class NotifyGlobalMapTravelContinued
    {
        [ProtoMember(1)]
        public NetworkGlobalMapTraveler Traveler { get; set; }
    }
}
