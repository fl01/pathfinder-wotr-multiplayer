namespace WOTRMultiplayer.Entities.Combat
{
    public abstract class NetworkCombatTurnBase
    {
        /// <summary>
        /// simple way to make sure turn is not going to end while we are busy processing something
        /// TODO: track exact reasons to make debugging easier?
        /// </summary>
        public int LockCounter { get; set; }
    }
}
