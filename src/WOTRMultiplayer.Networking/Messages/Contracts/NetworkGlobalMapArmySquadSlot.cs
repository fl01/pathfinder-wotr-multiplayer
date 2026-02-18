using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapArmySquadSlot
    {
        [ProtoMember(1)]
        [LogMe]
        public string SquadId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string ArmyId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkVector2Int Position { get; set; }
    }
}
