namespace WOTRMultiplayer.Abstractions.Hashing
{
    public interface IHashService
    {
        int Murmur3(string value);
    }
}
