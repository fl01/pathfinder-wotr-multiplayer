namespace WOTRMultiplayer.Entities.Combat.Crusades
{
    public class NetworkArmyCombatTurn
    {
        public string UnitId { get; set; }

        public bool IsInProgress { get; set; }

        public bool RequiresTurnEntitiesSynchronization { get; set; }

        public bool IsAI { get; set; }
    }
}
