using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAutopauseSettings
    {
        [ProtoMember(1)]
        [LogMe]
        public bool ContinueMovementOnEngagement { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool PauseOnAllyDown { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool PauseOnAreaLoaded { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool PauseOnAttackOfOpportunity { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public bool PauseOnEndedBuffSummon { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public bool PauseOnEndOfPartyMembersRound { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public bool PauseOnEndOfRound { get; set; }

        [ProtoMember(8)]
        [LogMe]
        public bool PauseOnEnemyDown { get; set; }

        [ProtoMember(9)]
        [LogMe]
        public bool PauseOnEnemySpotted { get; set; }

        [ProtoMember(10)]
        [LogMe]
        public bool PauseOnEngagement { get; set; }

        [ProtoMember(11)]
        [LogMe]
        public bool PauseOnHiddenObjectDetected { get; set; }

        [ProtoMember(12)]
        [LogMe]
        public bool PauseOnLostFocus { get; set; }

        [ProtoMember(13)]
        [LogMe]
        public bool PauseOnLowHealth { get; set; }

        [ProtoMember(14)]
        [LogMe]
        public bool PauseOnMeleeEngagement { get; set; }

        [ProtoMember(15)]
        [LogMe]
        public bool PauseOnNewEnemyAppeared { get; set; }

        [ProtoMember(16)]
        [LogMe]
        public bool PauseOnPartyIsAttacked { get; set; }

        [ProtoMember(17)]
        [LogMe]
        public bool PauseOnPartyMemberFinishedAbility { get; set; }

        [ProtoMember(18)]
        [LogMe]
        public bool PauseOnPartyMemberRanOutOfConsumable { get; set; }

        [ProtoMember(19)]
        [LogMe]
        public bool PauseOnSpellcastFinished { get; set; }

        [ProtoMember(20)]
        [LogMe]
        public string PauseOnSpellcastInterrupted { get; set; }

        [ProtoMember(21)]
        [LogMe]
        public string PauseOnSpellcastStarted { get; set; }

        [ProtoMember(22)]
        [LogMe]
        public bool PauseOnTrapDetected { get; set; }

        [ProtoMember(23)]
        [LogMe]
        public bool PauseOnWeaponIsIneffective { get; set; }

        [ProtoMember(24)]
        [LogMe]
        public bool PauseWhenAllyUnconscious { get; set; }

        [ProtoMember(25)]
        [LogMe]
        public bool PauseWhenEnemyUnconscious { get; set; }

        [ProtoMember(26)]
        [LogMe]
        public bool PauseWhenLastSleepingEnemyStays { get; set; }
    }
}
