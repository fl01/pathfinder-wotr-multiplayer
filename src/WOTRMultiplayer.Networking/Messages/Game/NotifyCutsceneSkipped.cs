using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCutsceneSkipped)]
    public class NotifyCutsceneSkipped
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
