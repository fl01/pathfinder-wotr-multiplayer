using System;
using WOTRMultiplayer.Networking;

namespace WOTRMultiplayer.Playground
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var networkClient = new NetworkServerClient();
            var client = new MultiplayerClient(networkClient);

            Console.ReadLine();
        }
    }
}
