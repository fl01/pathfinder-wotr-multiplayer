using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingNameChanged)]
    public class NotifyLevelingNameChanged
    {
        [ProtoMember(1)]
        public string Name { get; set; }
    }
}
