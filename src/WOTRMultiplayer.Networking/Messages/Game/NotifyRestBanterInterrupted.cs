using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyRestBanterInterrupted)]
    public class NotifyRestBanterInterrupted
    {
        [ProtoMember(1)]
        public NetworkRestBanter Banter { get; set; }
    }
}
