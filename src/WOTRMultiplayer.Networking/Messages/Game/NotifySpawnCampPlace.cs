using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifySpawnCampPlace)]
    public class NotifySpawnCampPlace
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkVector3 Position { get; set; }
    }
}
