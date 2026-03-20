using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingPortraitSelected)]
    public class NotifyLevelingPortraitSelected : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingPortrait Portrait { get; set; }
    }
}
