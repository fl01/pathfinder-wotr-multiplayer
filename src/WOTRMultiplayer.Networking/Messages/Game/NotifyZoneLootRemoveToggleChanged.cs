using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyZoneLootRemoveToggleChanged)]
    public class NotifyZoneLootRemoveToggleChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public bool RemoveLoot { get; set; }
    }
}
