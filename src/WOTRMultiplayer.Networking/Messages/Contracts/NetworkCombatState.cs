using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCombatState
    {
        [ProtoMember(1)]
        public int RoundNumber { get; set; }

        [ProtoMember(2)]
        public bool HasSurpriseRound { get; set; }

        [ProtoMember(3)]
        public List<NetworkUnit> Units { get; set; } = [];

        [ProtoMember(4)]
        public List<string> KilledUnits { get; set; } = [];
    }
}
