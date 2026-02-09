namespace WOTRMultiplayer.Services.Random
{
    public enum IdentifierLifetime
    {
        // single MP session including any quick loads
        Persistent,
        Area,
        Combat,
        CombatTurn
    }
}
