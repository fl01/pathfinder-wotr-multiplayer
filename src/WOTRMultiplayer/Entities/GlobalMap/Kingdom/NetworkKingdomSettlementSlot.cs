namespace WOTRMultiplayer.Entities.GlobalMap.Kingdom
{
    public class NetworkKingdomSettlementSlot
    {
        public string Id { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public override string ToString()
        {
            return $"{Id} <{X},{Y}>";
        }
    }
}
