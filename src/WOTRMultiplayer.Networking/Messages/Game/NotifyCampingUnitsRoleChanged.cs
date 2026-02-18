using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCampingUnitsRoleChanged)]
    public class NotifyCampingUnitsRoleChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public List<NetworkCampingRole> Roles { get; set; } = [];
    }
}
