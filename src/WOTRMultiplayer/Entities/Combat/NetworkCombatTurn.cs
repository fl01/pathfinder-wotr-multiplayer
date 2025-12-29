namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkCombatTurn
    {
        public string UnitId { get; set; }

        public bool IsAI { get; set; }

        public bool IsLocalPlayer { get; set; }

        public bool IsActingInSurpriseRound { get; set; }

        public bool IsInProgress { get; set; }

        public bool RequiresTurnEntitiesSynchronization { get; set; }

    }
}
