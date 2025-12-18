using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingPortraitSelected)]
    public class NotifyLevelingPortraitSelected
    {
        [ProtoMember(1)]
        public NetworkLevelingPortrait Portrait { get; set; }
    }
}
