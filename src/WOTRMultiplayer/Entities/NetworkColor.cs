namespace WOTRMultiplayer.Entities
{
    public class NetworkColor
    {
        public float R { get; set; }

        public float G { get; set; }

        public float B { get; set; }

        public float A { get; set; }

        public NetworkColor()
        {
        }

        public NetworkColor(float r, float g, float b)
            : this(r, g, b, 1f)
        {
        }

        public NetworkColor(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public override string ToString()
        {
            return $"({R},{G},{B},{A})";
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkColor other && other.R == R && other.G == G && other.B == B && other.A == A;
        }

        public override int GetHashCode()
        {
            // vector4.GetHashCode
            return R.GetHashCode() ^ (G.GetHashCode() << 2) ^ (B.GetHashCode() >> 2) ^ (A.GetHashCode() >> 1);
        }
    }
}
