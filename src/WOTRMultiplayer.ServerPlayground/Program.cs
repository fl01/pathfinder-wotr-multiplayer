using System;
using WOTRMultiplayer.Networking;

namespace WOTRMultiplayer.ServerPlayground
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine("Press enter to host");
            Console.ReadLine();

            var networkServer = new NetworkServer();
            var host = new MultiplayerHost(networkServer);
            host.Start(new MultiplayerSettings());
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
