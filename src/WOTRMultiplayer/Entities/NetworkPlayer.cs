using System.Collections.Generic;

namespace WOTRMultiplayer.Entities
{
    public class NetworkPlayer : IEqualityComparer<NetworkPlayer>, IEqualityComparer<long>
    {
        public long Id { get; private set; }

        public string Name { get; set; }

        public NetworkPlayerReadinessStatus Status { get; set; }

        public NetworkPlayer(long id)
        {
            Id = id;
        }

        public bool Equals(NetworkPlayer x, NetworkPlayer y)
        {
            return x != null && y != null && x.Id == y.Id;
        }

        public int GetHashCode(NetworkPlayer obj)
        {
            return obj.GetHashCode();
        }

        public bool Equals(long x, long y)
        {
            return x == y;
        }

        public int GetHashCode(long obj)
        {
            return obj.GetHashCode();
        }
    }
}
