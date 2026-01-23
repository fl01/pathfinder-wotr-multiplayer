using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapUnitsRecruited)]
    public class NotifyGlobalMapUnitsRecruited
    {
        [ProtoMember(1)]
        public NetworkGlobalMapUnitRecruitmentOrder Order { get; set; }
    }
}
