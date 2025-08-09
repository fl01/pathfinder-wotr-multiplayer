namespace WOTRMultiplayer.GameInteraction.Contexts
{
    public class PerceptionCheckRemoteContext
    {
        public string MapObjectId { get; set; }

        public string UnitId { get; set; }

        public PerceptionCheckRemoteContext(string unitId, string mapObjectId)
        {
            UnitId = unitId;
            MapObjectId = mapObjectId;
        }
    }
}
