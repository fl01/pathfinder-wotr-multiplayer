using Kingmaker.Settings;

namespace WOTRMultiplayer.Entities.Settings
{
    public class NetworkAutopauseSettings
    {
        public bool ContinueMovementOnEngagement { get; set; }

        public bool PauseOnAllyDown { get; set; }

        public bool PauseOnAreaLoaded { get; set; }

        public bool PauseOnAttackOfOpportunity { get; set; }

        public bool PauseOnEndedBuffSummon { get; set; }

        public bool PauseOnEndOfPartyMembersRound { get; set; }

        public bool PauseOnEndOfRound { get; set; }

        public bool PauseOnEnemyDown { get; set; }

        public bool PauseOnEnemySpotted { get; set; }

        public bool PauseOnEngagement { get; set; }

        public bool PauseOnHiddenObjectDetected { get; set; }

        public bool PauseOnLostFocus { get; set; }

        public bool PauseOnLowHealth { get; set; }

        public bool PauseOnMeleeEngagement { get; set; }

        public bool PauseOnNewEnemyAppeared { get; set; }

        public bool PauseOnPartyIsAttacked { get; set; }

        public bool PauseOnPartyMemberFinishedAbility { get; set; }

        public bool PauseOnPartyMemberRanOutOfConsumable { get; set; }

        public bool PauseOnSpellcastFinished { get; set; }

        public EntitiesType PauseOnSpellcastInterrupted { get; set; }

        public EntitiesType PauseOnSpellcastStarted { get; set; }

        public bool PauseOnTrapDetected { get; set; }

        public bool PauseOnWeaponIsIneffective { get; set; }

        public bool PauseWhenAllyUnconscious { get; set; }

        public bool PauseWhenEnemyUnconscious { get; set; }

        public bool PauseWhenLastSleepingEnemyStays { get; set; }
    }
}
