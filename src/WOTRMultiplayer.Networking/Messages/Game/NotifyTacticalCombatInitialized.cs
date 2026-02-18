using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyTacticalCombatInitialized)]
    public class NotifyTacticalCombatInitialized
    {
        [ProtoMember(1)]
        [LogMe]
        public int AreaSeed { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int Seed { get; set; }
    }
}
