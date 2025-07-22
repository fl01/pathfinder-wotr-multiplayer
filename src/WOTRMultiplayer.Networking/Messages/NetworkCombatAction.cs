using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages
{
    [ProtoContract]
    public class NetworkCombatAction
    {
        [ProtoMember(1)]
        public string MovementActivityStatePredicted { get; set; }

        [ProtoMember(2)]
        public string MovementActivityStateCurrent { get; set; }

        [ProtoMember(3)]
        public string AttackActivityStatePredicted { get; set; }

        [ProtoMember(4)]
        public string AttackActivityStateCurrent { get; set; }

        [ProtoMember(5)]
        public string AbilityActivityStatePredicted { get; set; }

        [ProtoMember(6)]
        public string AbilityActivityStateCurrent { get; set; }

        [ProtoMember(7)]
        public bool LockType { get; set; }

        [ProtoMember(8)]
        public bool HasMovePossibility { get; set; }

        [ProtoMember(9)]
        public float? MaxMoveDistance { get; set; }

        [ProtoMember(10)]
        public float? RemainingMoveDistance { get; set; }

        [ProtoMember(11)]
        public float? PredictedMoveDistance { get; set; }

        [ProtoMember(12)]
        public string Type { get; set; }
    }
}
