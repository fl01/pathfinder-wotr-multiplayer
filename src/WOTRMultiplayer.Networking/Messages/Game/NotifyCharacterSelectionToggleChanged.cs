using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCharacterSelectionToggleChanged)]
    public class NotifyCharacterSelectionToggleChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }
    }
}
