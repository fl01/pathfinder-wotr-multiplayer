using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifySpawnCampPlace)]
    public class NotifySpawnCampPlace
    {
        [ProtoMember(1)]
        public NetworkVector3 Position { get; set; }
    }
}
