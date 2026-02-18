using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCombatState
    {
        [ProtoMember(1)]
        [LogMe]
        public int RoundNumber { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool HasSurpriseRound { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public List<NetworkUnit> Units { get; set; } = [];

        [ProtoMember(4)]
        [LogMe]
        public List<NetworkAreaEffect> AreaEffects { get; set; } = [];
    }
}
