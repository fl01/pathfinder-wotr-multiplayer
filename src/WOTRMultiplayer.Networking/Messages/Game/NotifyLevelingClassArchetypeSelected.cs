using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingClassArchetypeSelected)]
    public class NotifyLevelingClassArchetypeSelected
    {
        [ProtoMember(1)]
        public string ArchetypeId { get; set; }
    }
}
