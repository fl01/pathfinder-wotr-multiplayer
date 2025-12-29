using System;
using System.Text;
using WOTRMultiplayer.Abstractions.Hashing;

namespace WOTRMultiplayer.Services.Hashing
{
    public class HashService : IHashService
    {
        public int Murmur3(string value)
        {
            var hashing = Murmur.MurmurHash.Create32();
            var data = Encoding.UTF8.GetBytes(value);
            var rawHash = hashing.ComputeHash(data);

            var hash = BitConverter.ToInt32(rawHash, 0);
            return hash;
        }
    }
}
