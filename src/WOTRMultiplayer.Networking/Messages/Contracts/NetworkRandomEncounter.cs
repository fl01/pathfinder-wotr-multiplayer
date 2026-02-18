using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkRandomEncounter
    {
        [ProtoMember(1)]
        [LogMe]
        public Dictionary<string, int> SpecialEncounters { get; set; } = [];

        [ProtoMember(2)]
        [LogMe]
        public float HoursPassedBeforeEncounter { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int GuardSlotRoll { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public int CamouflageRoll { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public int? RandomUnitSeed { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public Dictionary<string, float> PlaceUnitsInCampRangedRolls { get; set; } = [];

        [ProtoMember(7)]
        [LogMe]
        public Dictionary<string, string> PlaceUnitsInCampRangedTargetRolls { get; set; } = [];

        [ProtoMember(8)]
        [LogMe]
        public Dictionary<string, float> PlaceUnitsInCampUnitYRolls { get; set; } = [];

        [ProtoMember(9)]
        [LogMe]
        public Dictionary<string, float> PlaceUnitsInCampUnitEndPositionRolls { get; set; } = [];

        [ProtoMember(10)]
        [LogMe]
        public float PlaceUnitsOutsideOfCampSharedYRoll { get; set; }

        [ProtoMember(11)]
        [LogMe]
        public Dictionary<string, float> PlaceUnitsOutsideOfCampUnitYRolls { get; set; } = [];

        [ProtoMember(12)]
        [LogMe]
        public Dictionary<string, float> PlaceUnitsOutsideOfCampUnitEndPositionRolls { get; set; } = [];
    }
}
