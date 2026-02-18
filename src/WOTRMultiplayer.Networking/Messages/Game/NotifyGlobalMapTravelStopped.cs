using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapTravelStopped)]
    public class NotifyGlobalMapTravelStopped
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapTraveler Traveler { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public List<NetworkUnit> Party { get; set; }
    }
}
