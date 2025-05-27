using System;
using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.Networking;

namespace WOTRMultiplayer.Playground.Host
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine("Press enter to host");
            Console.ReadLine();

            var serviceProvider = DI.DIFactory.Create(new Config.UnityMod.UnityModManagerSettings { UseDebugConsole = false });
            var host = serviceProvider.GetService<IMultiplayerHost>();
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
