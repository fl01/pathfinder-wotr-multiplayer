using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCharacterLevelingStarted)]
    public class NotifyCharacterLevelingStarted
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
