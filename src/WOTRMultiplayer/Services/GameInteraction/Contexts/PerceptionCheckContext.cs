namespace WOTRMultiplayer.Services.GameInteraction.Contexts
{
    public class PerceptionCheckContext
    {
        public string MapObjectId { get; set; }

        public string UnitId { get; set; }

        public PerceptionCheckContext(string unitId, string mapObjectId)
        {
            UnitId = unitId;
            MapObjectId = mapObjectId;
        }
    }
}
