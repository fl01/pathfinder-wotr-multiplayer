using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingVoiceSelected)]
    public class NotifyLevelingVoiceSelected
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingVoice Voice { get; set; }
    }
}
