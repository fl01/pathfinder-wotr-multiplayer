using System;
using WOTRMultiplayer.Networking;

namespace WOTRMultiplayer.ClientPlayground
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine("Press enter to join");
            Console.ReadLine();

            var networkClient = new NetworkServerClient();
            var client = new MultiplayerClient(networkClient);
            client.Join("127.0.0.1:1024", null);
            var input = string.Empty;

            Console.Write(@$"
            exit - exit the program
            {Environment.NewLine}");
            while ((input = Console.ReadLine()) != "exit")
            {
                switch (input)
                {
                    case "1":
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
