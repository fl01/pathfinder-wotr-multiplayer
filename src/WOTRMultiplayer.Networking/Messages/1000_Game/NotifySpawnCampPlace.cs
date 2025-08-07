using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1033)]
    public class NotifySpawnCampPlace
    {
        [ProtoMember(1)]
        public NetworkVector3 Position { get; set; }
    }
}
