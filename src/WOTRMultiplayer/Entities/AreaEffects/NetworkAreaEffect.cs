using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.AreaEffects
{
    public class NetworkAreaEffect
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public NetworkVector3 Position { get; set; }

        public List<string> UnitsInside { get; set; } = [];

        public NetworkAreaEffectType Type { get; set; }

        public override string ToString()
        {
            return Id.ToString();
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkAreaEffect other && other.Id == this.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
